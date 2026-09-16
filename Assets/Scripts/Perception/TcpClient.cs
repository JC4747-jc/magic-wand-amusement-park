using System;
using System.Net.Sockets;
using System.Text;
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

        public void Configure(string host, int port, bool autoConnect = true)
        {
            if (!string.IsNullOrWhiteSpace(host))
                m_Host = host;
            if (port > 0)
                m_Port = port;
            m_AutoConnect = autoConnect;
        }

        void Start()
        {
            if (m_AutoConnect)
                ConnectIfNeeded();
        }

        /// <summary>Legacy sync connect on the calling thread (usually main). Unchanged behavior.</summary>
        public void ConnectIfNeeded()
        {
            if (IsConnected)
                return;

            try
            {
                DisconnectSocket();
                m_Client = new System.Net.Sockets.TcpClient();
                m_Client.NoDelay = true;
                m_Client.Connect(m_Host, m_Port);
                m_Stream = m_Client.GetStream();
                m_Buffer.Clear();
                Debug.Log(
                    $"[Bridge] Connected to {m_Host}:{m_Port} " +
                    "(PICO device: use `adb reverse tcp:5005 tcp:5005` when host is 127.0.0.1)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Bridge] Connect failed: {e.Message}");
                DisconnectSocket();
            }
        }

        public void DisconnectSocket()
        {
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
                return;

            try
            {
                while (m_Stream.DataAvailable)
                {
                    int count = m_Stream.Read(m_ReadBuf, 0, m_ReadBuf.Length);
                    if (count <= 0)
                        break;

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
