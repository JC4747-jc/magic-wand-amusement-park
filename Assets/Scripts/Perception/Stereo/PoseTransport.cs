using System;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SocketClient = System.Net.Sockets.TcpClient;

namespace Perception
{
    // One caller/in-flight operation. No Unity API, polling, synchronous network waits, or unbounded queue.
    public sealed class PoseTransport : IDisposable
    {
        readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        readonly object gate = new object();
        SocketClient connection;
        bool disposed;

        public async Task<string> ExchangeAsync(string host, int port, byte[] header,
            byte[] left, byte[] right, int deadlineMs)
        {
            using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                deadline.CancelAfter(deadlineMs);
                var token = deadline.Token;
                SocketClient client;
                lock (gate)
                {
                    if (disposed) throw new ObjectDisposedException(nameof(PoseTransport));
                    if (connection == null) connection = new SocketClient { NoDelay = true };
                    client = connection;
                }
                // Closing this operation's socket also cancels ConnectAsync on Unity's .NET profile.
                using (token.Register(() => client.Close()))
                try
                {
                    if (!client.Connected) await client.ConnectAsync(host, port).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    var stream = client.GetStream();
                    await WriteBlock(stream, header, token).ConfigureAwait(false);
                    if (left != null)
                    {
                        byte[] packedLeft = Compress(left, token), packedRight = Compress(right, token);
                        token.ThrowIfCancellationRequested();
                        await WriteBlock(stream, packedLeft, token).ConfigureAwait(false);
                        await WriteBlock(stream, packedRight, token).ConfigureAwait(false);
                    }
                    var prefix = await ReadExact(stream, 4, token).ConfigureAwait(false);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(prefix);
                    int size = BitConverter.ToInt32(prefix, 0);
                    if (size <= 0 || size > 65536) throw new IOException("Invalid pose reply length");
                    byte[] reply = await ReadExact(stream, size, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return Encoding.UTF8.GetString(reply);
                }
                catch
                {
                    client.Close();
                    lock (gate) { if (ReferenceEquals(connection, client)) connection = null; }
                    if (token.IsCancellationRequested) throw new OperationCanceledException("Pose request deadline or session cancelled", token);
                    throw;
                }
            }
        }
        static byte[] Compress(byte[] source, CancellationToken token)
        {
            using (var output = new MemoryStream())
            {
                using (var gzip = new GZipStream(output, CompressionLevel.Fastest, true))
                    for (int offset = 0; offset < source.Length; offset += 32768)
                    {
                        token.ThrowIfCancellationRequested();
                        gzip.Write(source, offset, Math.Min(32768, source.Length-offset));
                    }
                return output.ToArray();
            }
        }
        static async Task WriteBlock(NetworkStream stream, byte[] bytes, CancellationToken token)
        {
            byte[] prefix = BitConverter.GetBytes(bytes.Length);
            if (!BitConverter.IsLittleEndian) Array.Reverse(prefix);
            await stream.WriteAsync(prefix, 0, 4, token).ConfigureAwait(false);
            await stream.WriteAsync(bytes, 0, bytes.Length, token).ConfigureAwait(false);
        }
        static async Task<byte[]> ReadExact(NetworkStream stream, int count, CancellationToken token)
        {
            var bytes = new byte[count]; int offset = 0;
            while (offset < count)
            {
                int read = await stream.ReadAsync(bytes, offset, count-offset, token).ConfigureAwait(false);
                if (read == 0) throw new EndOfStreamException("Pose server closed connection");
                offset += read;
            }
            return bytes;
        }
        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                lifetime.Cancel(); connection?.Close(); connection = null;
            }
            // CTS remains owned by cancelled operations until their continuations finish.
        }
    }

    public sealed class PoseRetryPolicy
    {
        public int Failures { get; private set; }
        public double NextAttempt { get; private set; }
        public bool CircuitOpen => Failures >= 3;
        public bool CanAttempt(double now) => now >= NextAttempt;
        public void Fail(double now)
        {
            Failures = Math.Min(Failures+1, 30);
            NextAttempt = now + Math.Min(8, Math.Pow(2, Math.Min(Failures-1, 3)));
        }
        public void Reset() { Failures = 0; NextAttempt = 0; }
    }
}
