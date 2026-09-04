using System;
using System.Buffers;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// Encodes camera / test Texture2D frames to JPEG and sends PICF packets to the PC YOLO bridge.
    /// Android: PicoCameraCapture.FrameUpdated → TrySendTexture.
    /// Editor: PicoYoloEditorTestSource → TrySendTexture (same encode/PICF/TCP path).
    /// </summary>
    public class PicoYoloFrameSender : MonoBehaviour
    {
        public const uint ProtocolVersion = 1;
        public const int PicfHeaderSize = 29;
        public static readonly byte[] Magic = { (byte)'P', (byte)'I', (byte)'C', (byte)'F' };

        [SerializeField]
        PicoCameraCapture m_Capture;

        [SerializeField]
        TcpClient m_TcpClient;

        [SerializeField]
        [Range(30, 95)]
        int m_JpegQuality = 70;

        [SerializeField]
        [Tooltip("If the longer edge exceeds this, downscale before JPEG. 0 = native resolution.")]
        int m_MaxLongEdge = 640;

        [SerializeField]
        [Tooltip("Rate-limit for PicoCameraCapture.FrameUpdated path. Editor test source uses its own FPS.")]
        float m_MaxSendFps = 5f;

        [SerializeField]
        [Tooltip("If YOLO boxes look vertically flipped vs Preview, enable this.")]
        bool m_FlipVerticallyBeforeEncode;

        [SerializeField]
        bool m_LogEveryFrame = true;

        uint m_NextFrameId = 1;
        float m_NextSendTime;
        bool m_SendBusy;
        Texture2D m_ScratchReadable;
        Texture2D m_FlipScratch;
        byte[] m_PicfPacketBuffer;

        public uint LastSentFrameId { get; private set; }
        public int LastSentWidth { get; private set; }
        public int LastSentHeight { get; private set; }
        public int LastJpegBytes { get; private set; }
        public bool FlipVerticallyBeforeEncode => m_FlipVerticallyBeforeEncode;
        public int MaxLongEdge => m_MaxLongEdge;

        /// <summary>Orientation notes recorded once for Phase 2.</summary>
        public static string OrientationAuditNotes { get; private set; } =
            "PXR raw RGBA copied row0→texture without flip; Unity EncodeToJPG uses Texture2D storage; " +
            "pixel origin assumed top-left in YOLO/OpenCV after imdecode; " +
            "rotation/mirror not corrected in Phase 1 (no Intrinsics).";

        void Awake()
        {
            if (m_Capture == null)
                m_Capture = GetComponent<PicoCameraCapture>();
            if (m_Capture == null)
                m_Capture = FindFirstObjectByType<PicoCameraCapture>();
            if (m_TcpClient == null)
                m_TcpClient = FindFirstObjectByType<TcpClient>();
        }

        void OnEnable()
        {
            if (m_Capture != null)
                m_Capture.FrameUpdated += OnCaptureFrame;
            else
                Debug.LogWarning(
                    "[PicoYoloFrameSender] PicoCameraCapture missing — " +
                    "device capture path inactive; Editor can still call TrySendTexture.");

            if (m_TcpClient == null)
                Debug.LogError("[PicoYoloFrameSender] TcpClient missing.");
        }

        void OnDisable()
        {
            if (m_Capture != null)
                m_Capture.FrameUpdated -= OnCaptureFrame;
        }

        void OnDestroy()
        {
            if (m_ScratchReadable != null)
                Destroy(m_ScratchReadable);
            if (m_FlipScratch != null)
                Destroy(m_FlipScratch);
            if (m_PicfPacketBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(m_PicfPacketBuffer);
                m_PicfPacketBuffer = null;
            }
        }

        void OnCaptureFrame(Texture2D texture)
        {
            TrySendTexture(texture, respectSenderRateLimit: true);
        }

        /// <summary>
        /// Shared entry for PICO capture and Editor test images.
        /// Reuses JPEG encode, resize, quality, frame_id, timestamp, PICF, TcpClient.
        /// </summary>
        /// <param name="texture">Source texture (PICO frame or Editor test image).</param>
        /// <param name="respectSenderRateLimit">
        /// True for PicoCameraCapture path (uses m_MaxSendFps).
        /// False when caller already rate-limits (Editor test source).
        /// </param>
        public bool TrySendTexture(Texture2D texture, bool respectSenderRateLimit = true)
        {
            if (texture == null || m_TcpClient == null || !m_TcpClient.IsConnected)
                return false;
            if (m_SendBusy)
                return false;

            if (respectSenderRateLimit)
            {
                if (Time.unscaledTime < m_NextSendTime)
                    return false;
                float interval = m_MaxSendFps <= 0.1f ? 0.2f : 1f / m_MaxSendFps;
                m_NextSendTime = Time.unscaledTime + interval;
            }

            m_SendBusy = true;
            try
            {
                return SendFrame(texture);
            }
            finally
            {
                m_SendBusy = false;
            }
        }

        bool SendFrame(Texture2D source)
        {
            Texture2D encodeSource = PrepareEncodeTexture(source, out int width, out int height);
            if (encodeSource == null)
                return false;

            byte[] jpeg;
            try
            {
                jpeg = encodeSource.EncodeToJPG(m_JpegQuality);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PicoYoloFrameSender] EncodeToJPG failed: {e.Message}");
                return false;
            }

            if (jpeg == null || jpeg.Length == 0)
            {
                Debug.LogWarning("[PicoYoloFrameSender] EncodeToJPG produced empty buffer.");
                return false;
            }

            uint frameId = m_NextFrameId++;
            long timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            int packetLen = PicfHeaderSize + jpeg.Length;
            byte[] packet = RentPicfPacketBuffer(packetLen);
            int o = 0;
            Buffer.BlockCopy(Magic, 0, packet, o, 4);
            o += 4;
            packet[o++] = (byte)ProtocolVersion;
            WriteU32LE(packet, ref o, frameId);
            WriteI64LE(packet, ref o, timestampMs);
            WriteU32LE(packet, ref o, (uint)width);
            WriteU32LE(packet, ref o, (uint)height);
            WriteU32LE(packet, ref o, (uint)jpeg.Length);
            Buffer.BlockCopy(jpeg, 0, packet, o, jpeg.Length);

            if (!m_TcpClient.TrySend(packet, 0, packetLen))
                return false;

            LastSentFrameId = frameId;
            LastSentWidth = width;
            LastSentHeight = height;
            LastJpegBytes = jpeg.Length;

            if (m_LogEveryFrame)
            {
                Debug.Log(
                    $"[Legacy] frame sent id={frameId} {width}x{height} " +
                    $"jpeg={jpeg.Length}B quality={m_JpegQuality} flipV={m_FlipVerticallyBeforeEncode}");
            }

            return true;
        }

        byte[] RentPicfPacketBuffer(int requiredLength)
        {
            if (m_PicfPacketBuffer != null && m_PicfPacketBuffer.Length >= requiredLength)
                return m_PicfPacketBuffer;

            if (m_PicfPacketBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(m_PicfPacketBuffer);
                m_PicfPacketBuffer = null;
            }

            m_PicfPacketBuffer = ArrayPool<byte>.Shared.Rent(requiredLength);
            return m_PicfPacketBuffer;
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

            // Imported Editor textures often have Read/Write off; Blit makes EncodeToJPG safe.
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
