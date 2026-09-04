using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Perception
{
    /// <summary>
    /// Phase 3: real TCP client for PICF → PC YOLO → newline JSON → DetectionFrame.
    /// Network I/O on a background worker; DetectionReceived only on Unity main thread.
    /// JPEG EncodeToJPG stays on main thread (temporary bottleneck — does not Wait for YOLO).
    /// </summary>
    public class RemoteInferenceClient : MonoBehaviour, IInferenceClient
    {
        const uint ProtocolVersion = PicoYoloFrameSender.ProtocolVersion;

        enum WorkerCommand : byte
        {
            None = 0,
            Connect = 1,
            Disconnect = 2,
            Shutdown = 3
        }

        struct OutboundPacket
        {
            public byte[] Data;
            public uint FrameId;
            public long SendTimestampTicks;
        }

        struct ParsedDetection
        {
            public DetectionFrame Frame;
            public long ReceiveTimestampTicks;
        }

        [Header("TCP")]
        [SerializeField]
        string m_Host = "127.0.0.1";

        [SerializeField]
        int m_Port = 5005;

        [Header("Encode (main thread)")]
        [SerializeField]
        [Range(30, 95)]
        int m_JpegQuality = 70;

        [SerializeField]
        int m_MaxLongEdge = 640;

        [SerializeField]
        bool m_FlipVerticallyBeforeEncode;

        [Header("Frame source")]
        [SerializeField]
        PicoCameraFrameProvider m_FrameProvider;

        [SerializeField]
        bool m_AutoSubmitFromProvider = true;

        [Header("Debug")]
        [SerializeField]
        bool m_LogEveryFrame = true;

        [SerializeField]
        float m_MinConfidence;

        readonly InferenceTelemetry m_Telemetry = new InferenceTelemetry();
        readonly ConcurrentQueue<OutboundPacket> m_SendQueue = new ConcurrentQueue<OutboundPacket>();
        readonly ConcurrentQueue<ParsedDetection> m_MainThreadDetections = new ConcurrentQueue<ParsedDetection>();
        readonly ConcurrentQueue<Action> m_MainThreadActions = new ConcurrentQueue<Action>();
        readonly ConcurrentDictionary<uint, long> m_SendTicksByFrameId = new ConcurrentDictionary<uint, long>();

        CameraFrame m_PendingFrame;
        bool m_HasPending;
        bool m_InFlight;
        uint m_NextSendFrameId = 1;

        Thread m_Worker;
        volatile WorkerCommand m_Command;
        volatile bool m_WorkerRunning;
        volatile InferenceConnectionState m_ConnectionState = InferenceConnectionState.Disconnected;
        volatile string m_LastError = string.Empty;

        Texture2D m_ScratchReadable;
        Texture2D m_FlipScratch;

        int m_SendCountWindow;
        int m_DetectCountWindow;
        float m_FpsWindowStart;

        public bool IsConnected => ConnectionState == InferenceConnectionState.Connected;

        public InferenceConnectionState ConnectionState
        {
            get => m_ConnectionState;
            private set => m_ConnectionState = value;
        }

        public InferenceTelemetry Telemetry => m_Telemetry;

        public event Action<DetectionFrame> DetectionReceived;
        public event Action<InferenceTelemetry> TelemetryUpdated;

        void Awake()
        {
            if (m_FrameProvider == null)
                m_FrameProvider = GetComponent<PicoCameraFrameProvider>();
            if (m_FrameProvider == null)
                m_FrameProvider = FindFirstObjectByType<PicoCameraFrameProvider>();
        }

        void OnEnable()
        {
            EnsureWorker();
            if (m_AutoSubmitFromProvider && m_FrameProvider != null)
                m_FrameProvider.FrameAvailable += OnFrameAvailable;
        }

        void OnDisable()
        {
            if (m_FrameProvider != null)
                m_FrameProvider.FrameAvailable -= OnFrameAvailable;
            Disconnect();
        }

        void OnDestroy()
        {
            ShutdownWorker();
            if (m_ScratchReadable != null)
                Destroy(m_ScratchReadable);
            if (m_FlipScratch != null)
                Destroy(m_FlipScratch);
        }

        void OnFrameAvailable(CameraFrame frame)
        {
            if (!isActiveAndEnabled || !m_AutoSubmitFromProvider)
                return;
            TrySubmitFrame(frame);
        }

        public void Connect()
        {
            EnsureWorker();
            SetConnectionState(InferenceConnectionState.Connecting, clearError: true);
            m_Command = WorkerCommand.Connect;
            Debug.Log($"[RemoteInference] Connect requested → {m_Host}:{m_Port} (background)");
        }

        public void Disconnect()
        {
            m_Command = WorkerCommand.Disconnect;
            ClearFlightState();
            SetConnectionState(InferenceConnectionState.Disconnected, clearError: false);
            while (m_SendQueue.TryDequeue(out _))
            {
            }

            m_Telemetry.IsConnected = false;
            m_Telemetry.ConnectionState = InferenceConnectionState.Disconnected;
            m_Telemetry.PendingFrames = 0;
        }

        public bool TrySubmitFrame(CameraFrame frame)
        {
            if (!frame.IsValid)
                return false;
            if (!IsConnected)
                return false;

            // Latest-frame wins (main-thread owned slots).
            if (m_InFlight)
            {
                if (m_HasPending)
                    m_Telemetry.DroppedFrames++;
                m_PendingFrame = frame;
                m_HasPending = true;
                m_Telemetry.PendingFrames = 1;
                return true;
            }

            return BeginSend(frame);
        }

        bool BeginSend(CameraFrame frame)
        {
            // TEMP BOTTLENECK: EncodeToJPG on main thread. Does not wait on network/YOLO.
            if (!TryEncodeAndBuildPacket(frame.Texture, out byte[] packet, out uint frameId, out int width, out int height))
                return false;

            long ticks = Stopwatch.GetTimestamp();
            m_SendTicksByFrameId[frameId] = ticks;
            m_SendQueue.Enqueue(new OutboundPacket
            {
                Data = packet,
                FrameId = frameId,
                SendTimestampTicks = ticks
            });

            m_InFlight = true;
            m_HasPending = false;
            m_Telemetry.PendingFrames = 0;
            m_Telemetry.LastSubmittedFrameId = frameId;
            m_SendCountWindow++;

            if (m_LogEveryFrame)
            {
                Debug.Log(
                    $"[RemoteInference] Frame sent: {frameId} {width}x{height} " +
                    $"jpeg={packet.Length - 29}B (encode=main, write=worker)");
            }

            return true;
        }

        void Update()
        {
            while (m_MainThreadActions.TryDequeue(out Action action))
            {
                try
                {
                    action?.Invoke();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[RemoteInference] Main action error: {e.Message}");
                }
            }

            bool gotDetection = false;
            while (m_MainThreadDetections.TryDequeue(out ParsedDetection parsed))
            {
                gotDetection = true;
                DetectionFrame frame = parsed.Frame;
                m_Telemetry.LastDetectionFrameId = frame.frameId;
                m_DetectCountWindow++;

                if (m_SendTicksByFrameId.TryRemove((uint)frame.frameId, out long sendTicks))
                {
                    double ms = (parsed.ReceiveTimestampTicks - sendTicks) * 1000.0 / Stopwatch.Frequency;
                    m_Telemetry.RoundTripLatencyMs = (float)ms;
                }

                if (m_LogEveryFrame)
                {
                    Debug.Log(
                        $"[RemoteInference] Detection received: frameId={frame.frameId} " +
                        $"count={frame.Count} RTT={m_Telemetry.RoundTripLatencyMs:F1}ms " +
                        $"Dropped={m_Telemetry.DroppedFrames} Pending={m_Telemetry.PendingFrames}");
                }

                DetectionReceived?.Invoke(frame);

                // One PICF → one JSON: clear in-flight and promote pending on main thread.
                m_InFlight = false;
                PromotePendingIfAny();
            }

            UpdateFpsWindow();

            m_Telemetry.IsConnected = IsConnected;
            m_Telemetry.ConnectionState = ConnectionState;
            m_Telemetry.LastError = m_LastError;

            if (gotDetection)
                TelemetryUpdated?.Invoke(m_Telemetry.Clone());
        }

        void PromotePendingIfAny()
        {
            if (!m_HasPending || m_InFlight || !IsConnected)
                return;

            m_HasPending = false;
            m_Telemetry.PendingFrames = 0;
            CameraFrame next = m_PendingFrame;
            BeginSend(next);
        }

        void ClearFlightState()
        {
            m_InFlight = false;
            m_HasPending = false;
            m_Telemetry.PendingFrames = 0;
            m_SendTicksByFrameId.Clear();
        }

        void UpdateFpsWindow()
        {
            if (m_FpsWindowStart <= 0f)
                m_FpsWindowStart = Time.unscaledTime;

            float elapsed = Time.unscaledTime - m_FpsWindowStart;
            if (elapsed < 1f)
                return;

            m_Telemetry.SendFps = m_SendCountWindow / elapsed;
            m_Telemetry.InferenceFps = m_DetectCountWindow / elapsed;
            m_SendCountWindow = 0;
            m_DetectCountWindow = 0;
            m_FpsWindowStart = Time.unscaledTime;

            if (m_LogEveryFrame && IsConnected)
            {
                Debug.Log(
                    $"[RemoteInference] Telemetry SendFPS={m_Telemetry.SendFps:F1} " +
                    $"DetectionFPS={m_Telemetry.InferenceFps:F1} " +
                    $"RTT={m_Telemetry.RoundTripLatencyMs:F1}ms " +
                    $"Dropped={m_Telemetry.DroppedFrames} Pending={m_Telemetry.PendingFrames} " +
                    $"LastSubmit={m_Telemetry.LastSubmittedFrameId} LastDet={m_Telemetry.LastDetectionFrameId}");
            }
        }

        void SetConnectionState(InferenceConnectionState state, bool clearError)
        {
            ConnectionState = state;
            m_Telemetry.ConnectionState = state;
            m_Telemetry.IsConnected = state == InferenceConnectionState.Connected;
            if (clearError)
            {
                m_LastError = string.Empty;
                m_Telemetry.LastError = string.Empty;
            }
        }

        void EnsureWorker()
        {
            if (m_Worker != null && m_Worker.IsAlive)
                return;

            m_WorkerRunning = true;
            m_Worker = new Thread(WorkerLoop)
            {
                Name = "RemoteInferenceClient",
                IsBackground = true
            };
            m_Worker.Start();
        }

        void ShutdownWorker()
        {
            m_Command = WorkerCommand.Shutdown;
            m_WorkerRunning = false;
            if (m_Worker != null && m_Worker.IsAlive)
            {
                if (!m_Worker.Join(1000))
                    Debug.LogWarning("[RemoteInference] Worker join timed out.");
            }

            m_Worker = null;
        }

        void WorkerLoop()
        {
            System.Net.Sockets.TcpClient client = null;
            NetworkStream stream = null;
            var readBuffer = new byte[8192];
            var textBuffer = new StringBuilder();

            try
            {
                while (m_WorkerRunning)
                {
                    WorkerCommand cmd = m_Command;
                    if (cmd == WorkerCommand.Shutdown)
                        break;

                    if (cmd == WorkerCommand.Disconnect)
                    {
                        m_Command = WorkerCommand.None;
                        CloseSocket(ref client, ref stream);
                        PostState(InferenceConnectionState.Disconnected, null);
                        Thread.Sleep(20);
                        continue;
                    }

                    if (cmd == WorkerCommand.Connect)
                    {
                        m_Command = WorkerCommand.None;
                        CloseSocket(ref client, ref stream);
                        PostState(InferenceConnectionState.Connecting, null);
                        try
                        {
                            client = new System.Net.Sockets.TcpClient();
                            client.NoDelay = true;
                            // Background Connect — never call from Update.
                            client.Connect(m_Host, m_Port);
                            stream = client.GetStream();
                            stream.ReadTimeout = 50;
                            stream.WriteTimeout = 5000;
                            textBuffer.Clear();
                            PostState(InferenceConnectionState.Connected, null);
                            PostLog(
                                $"[RemoteInference] Connected = true ({m_Host}:{m_Port}). " +
                                "Use adb reverse tcp:5005 tcp:5005 on device.");
                        }
                        catch (Exception e)
                        {
                            CloseSocket(ref client, ref stream);
                            PostState(InferenceConnectionState.Faulted, e.Message);
                            PostLog($"[RemoteInference] Connect failed: {e.Message}");
                        }
                    }

                    if (stream == null || client == null || !client.Connected)
                    {
                        Thread.Sleep(20);
                        continue;
                    }

                    try
                    {
                        while (m_SendQueue.TryDequeue(out OutboundPacket packet))
                        {
                            stream.Write(packet.Data, 0, packet.Data.Length);
                            stream.Flush();
                        }

                        DrainReads(stream, readBuffer, textBuffer);
                    }
                    catch (Exception e)
                    {
                        PostState(InferenceConnectionState.Faulted, e.Message);
                        PostLog($"[RemoteInference] IO error: {e.Message}");
                        CloseSocket(ref client, ref stream);
                        PostMain(() =>
                        {
                            ClearFlightState();
                        });
                    }
                }
            }
            finally
            {
                CloseSocket(ref client, ref stream);
            }
        }

        void DrainReads(NetworkStream stream, byte[] readBuffer, StringBuilder textBuffer)
        {
            // Prefer DataAvailable to avoid long blocks; ReadTimeout=50 as fallback.
            for (int iter = 0; iter < 32; iter++)
            {
                int count;
                try
                {
                    if (!stream.DataAvailable)
                    {
                        // Brief blocking read so we still wake when PC responds without spin.
                        count = stream.Read(readBuffer, 0, readBuffer.Length);
                    }
                    else
                    {
                        count = stream.Read(readBuffer, 0, readBuffer.Length);
                    }
                }
                catch (IOException)
                {
                    // Read timeout — normal when idle.
                    return;
                }
                catch (SocketException)
                {
                    return;
                }

                if (count <= 0)
                    return;

                textBuffer.Append(Encoding.UTF8.GetString(readBuffer, 0, count));

                while (true)
                {
                    string all = textBuffer.ToString();
                    int newline = all.IndexOf('\n');
                    if (newline < 0)
                        break;

                    string line = all.Substring(0, newline).Trim('\r');
                    textBuffer.Remove(0, newline + 1);
                    if (string.IsNullOrEmpty(line))
                        continue;

                    long recvTicks = Stopwatch.GetTimestamp();
                    if (!DetectionJsonParser.TryParse(line, m_MinConfidence, out DetectionFrame frame))
                    {
                        PostLog($"[RemoteInference] Failed to parse JSON line ({line.Length} chars)");
                        continue;
                    }

                    m_MainThreadDetections.Enqueue(new ParsedDetection
                    {
                        Frame = frame,
                        ReceiveTimestampTicks = recvTicks
                    });
                }

                if (!stream.DataAvailable)
                    break;
            }
        }

        void PostState(InferenceConnectionState state, string error)
        {
            PostMain(() =>
            {
                if (error != null)
                {
                    m_LastError = error;
                    m_Telemetry.LastError = error;
                }

                SetConnectionState(state, clearError: error == null && state == InferenceConnectionState.Connected);
                TelemetryUpdated?.Invoke(m_Telemetry.Clone());
            });
        }

        void PostLog(string message)
        {
            PostMain(() => Debug.Log(message));
        }

        void PostMain(Action action)
        {
            if (action != null)
                m_MainThreadActions.Enqueue(action);
        }

        static void CloseSocket(ref System.Net.Sockets.TcpClient client, ref NetworkStream stream)
        {
            try
            {
                stream?.Close();
            }
            catch
            {
                // ignore
            }

            try
            {
                client?.Close();
            }
            catch
            {
                // ignore
            }

            stream = null;
            client = null;
        }

        bool TryEncodeAndBuildPacket(
            Texture2D source,
            out byte[] packet,
            out uint frameId,
            out int width,
            out int height)
        {
            packet = null;
            frameId = 0;
            width = 0;
            height = 0;

            Texture2D encodeSource = PrepareEncodeTexture(source, out width, out height);
            if (encodeSource == null)
                return false;

            byte[] jpeg;
            try
            {
                jpeg = encodeSource.EncodeToJPG(m_JpegQuality);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[RemoteInference] EncodeToJPG failed: {e.Message}");
                return false;
            }

            if (jpeg == null || jpeg.Length == 0)
                return false;

            frameId = m_NextSendFrameId++;
            long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            // Exact PICF layout from PicoYoloFrameSender (29-byte header + jpeg).
            packet = new byte[29 + jpeg.Length];
            int o = 0;
            Buffer.BlockCopy(PicoYoloFrameSender.Magic, 0, packet, o, 4);
            o += 4;
            packet[o++] = (byte)ProtocolVersion;
            WriteU32LE(packet, ref o, frameId);
            WriteI64LE(packet, ref o, timestampMs);
            WriteU32LE(packet, ref o, (uint)width);
            WriteU32LE(packet, ref o, (uint)height);
            WriteU32LE(packet, ref o, (uint)jpeg.Length);
            Buffer.BlockCopy(jpeg, 0, packet, o, jpeg.Length);
            return true;
        }

        Texture2D PrepareEncodeTexture(Texture2D source, out int width, out int height)
        {
            width = source.width;
            height = source.height;
            if (width <= 0 || height <= 0)
                return null;

            Texture2D working = source;
            bool needGpuCopy = !source.isReadable;

            if (m_MaxLongEdge > 0)
            {
                int longEdge = Mathf.Max(width, height);
                if (longEdge > m_MaxLongEdge)
                {
                    float scale = m_MaxLongEdge / (float)longEdge;
                    int nw = Mathf.Max(1, Mathf.RoundToInt(width * scale));
                    int nh = Mathf.Max(1, Mathf.RoundToInt(height * scale));
                    working = BlitCopy(source, nw, nh);
                    width = nw;
                    height = nh;
                    needGpuCopy = false;
                }
            }

            if (needGpuCopy)
                working = BlitCopy(source, width, height);

            if (m_FlipVerticallyBeforeEncode)
                working = FlipVertical(working);

            return working;
        }

        Texture2D BlitCopy(Texture source, int width, int height)
        {
            RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            RenderTexture prev = RenderTexture.active;
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            EnsureScratch(ref m_ScratchReadable, width, height);
            m_ScratchReadable.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            m_ScratchReadable.Apply(false, false);

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return m_ScratchReadable;
        }

        Texture2D FlipVertical(Texture2D source)
        {
            int w = source.width;
            int h = source.height;
            Color32[] src = source.GetPixels32();
            Color32[] dst = new Color32[src.Length];
            for (int y = 0; y < h; y++)
            {
                int srcRow = y * w;
                int dstRow = (h - 1 - y) * w;
                Array.Copy(src, srcRow, dst, dstRow, w);
            }

            EnsureScratch(ref m_FlipScratch, w, h);
            m_FlipScratch.SetPixels32(dst);
            m_FlipScratch.Apply(false, false);
            return m_FlipScratch;
        }

        static void EnsureScratch(ref Texture2D tex, int width, int height)
        {
            if (tex != null && tex.width == width && tex.height == height)
                return;
            if (tex != null)
                Destroy(tex);
            tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        }

        static void WriteU32LE(byte[] buf, ref int offset, uint value)
        {
            buf[offset++] = (byte)(value & 0xff);
            buf[offset++] = (byte)((value >> 8) & 0xff);
            buf[offset++] = (byte)((value >> 16) & 0xff);
            buf[offset++] = (byte)((value >> 24) & 0xff);
        }

        static void WriteI64LE(byte[] buf, ref int offset, long value)
        {
            ulong u = unchecked((ulong)value);
            for (int i = 0; i < 8; i++)
            {
                buf[offset++] = (byte)(u & 0xff);
                u >>= 8;
            }
        }
    }
}
