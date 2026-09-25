using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Perception
{
    /// <summary>
    /// TCP transport: connect to PC YOLO bridge, send framed PICO JPEG, receive JSON lines.
    /// Legacy path only. Remote path uses RemoteInferenceClient (do not dual-connect).
    /// </summary>
    public class TcpClient : MonoBehaviour
    {
        [SerializeField]
        string m_Host = "127.0.0.1";

        [SerializeField]
        int m_Port = 5005;

        [SerializeField]
        [Tooltip("When false, Start() skips connect. Used by InferenceBackendSwitcher for Remote mode.")]
        bool m_AutoConnect = true;

        [SerializeField] bool m_AutoReconnect;
        bool m_Connecting;
        int m_ConnectionRevision;
        float m_NextConnectTime;

        System.Net.Sockets.TcpClient m_Client;
        NetworkStream m_Stream;
        readonly StringBuilder m_Buffer = new StringBuilder();
        readonly byte[] m_ReadBuf = new byte[8192];
        readonly object m_SendLock = new object();

        /// <summary>Fired for each complete newline-delimited JSON string.</summary>
        public event Action<string> LineReceived;

        public bool IsConnected => m_Client != null && m_Client.Connected;
        public string Host => m_Host;
        public int Port => m_Port;

        public bool AutoConnect
        {
            get => m_AutoConnect;
            set => m_AutoConnect = value;
        }

        void Start()
        {
            if (m_AutoConnect)
                ConnectIfNeeded();
        }

        /// <summary>Connect without blocking the XR rendering thread; stale attempts cannot replace a newer socket.</summary>
        public async void ConnectIfNeeded()
        {
            if (IsConnected || m_Connecting)
                return;
            DisconnectSocket();
            int revision = m_ConnectionRevision;
            m_Connecting = true;
            var candidate = new System.Net.Sockets.TcpClient { NoDelay = true };
            bool adopted = false;
            try
            {
                var connect = candidate.ConnectAsync(m_Host, m_Port);
                if (await Task.WhenAny(connect, Task.Delay(1500)) != connect)
                {
                    candidate.Close();
                    try { await connect; } catch { }
                    throw new TimeoutException("PC connection timed out");
                }
                await connect;
                if (revision != m_ConnectionRevision || this == null) return;
                m_Client = candidate;
                m_Stream = m_Client.GetStream();
                m_Stream.WriteTimeout = 500;
                m_Stream.ReadTimeout = 500;
                adopted = true;
                m_Buffer.Clear();
                Debug.Log(
                    $"[Bridge] Connected to {m_Host}:{m_Port} " +
                    "(PICO device: use `adb reverse tcp:5005 tcp:5005` when host is 127.0.0.1)");
            }
            catch (Exception e)
            {
                if (revision == m_ConnectionRevision)
                {
                    Debug.LogWarning($"[Bridge] Connect failed: {e.Message}");
                    DisconnectSocket();
                }
            }
            finally
            {
                if (!adopted) candidate.Close();
                if (revision == m_ConnectionRevision) m_Connecting = false;
            }
        }

        public void DisconnectSocket()
        {
            m_ConnectionRevision++;
            m_Connecting = false;
            m_NextConnectTime = Time.unscaledTime + 2f;
            try
            {
                m_Stream?.Close();
            }
            catch
            {
                // ignore
            }

            try
            {
                m_Client?.Close();
            }
            catch
            {
                // ignore
            }

            m_Stream = null;
            m_Client = null;
            m_Buffer.Clear();
        }

        void Update()
        {
            if (m_Stream == null)
            {
                if (m_AutoReconnect && !m_Connecting && Time.unscaledTime >= m_NextConnectTime)
                    ConnectIfNeeded();
                return;
            }

            try
            {
                if (m_Client.Client.Poll(0, SelectMode.SelectRead) && m_Client.Available == 0)
                { DisconnectSocket(); return; }
                while (m_Stream.DataAvailable)
                {
                    int count = m_Stream.Read(m_ReadBuf, 0, m_ReadBuf.Length);
                    if (count <= 0) { DisconnectSocket(); return; }

                    m_Buffer.Append(Encoding.UTF8.GetString(m_ReadBuf, 0, count));

                    while (true)
                    {
                        string all = m_Buffer.ToString();
                        int newline = all.IndexOf('\n');
                        if (newline < 0)
                            break;

                        string line = all.Substring(0, newline).Trim('\r');
                        m_Buffer.Remove(0, newline + 1);

                        if (!string.IsNullOrEmpty(line))
                            LineReceived?.Invoke(line);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Bridge] Read error: {e.Message}");
                DisconnectSocket();
            }
        }

        /// <summary>Sends a framed PICF JPEG payload (Unity → Python).</summary>
        public bool TrySend(byte[] data, int offset, int count)
        {
            if (m_Stream == null || data == null || count <= 0)
                return false;

            lock (m_SendLock)
            {
                try
                {
                    m_Stream.Write(data, offset, count);
                    m_Stream.Flush();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Bridge] Send failed: {e.Message}");
                    DisconnectSocket();
                    return false;
                }
            }
        }

        public bool TrySend(byte[] data) =>
            data != null && TrySend(data, 0, data.Length);

        void OnDestroy()
        {
            DisconnectSocket();
        }
    }
}
