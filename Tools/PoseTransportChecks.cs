using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Perception;

// Real loopback sockets exercise the production C# transport, without a Unity rendering loop.
class PoseTransportChecks
{
    static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    static async Task<byte[]> Read(NetworkStream stream, int count)
    {
        var data = new byte[count]; int offset = 0;
        while (offset < count) { int n = await stream.ReadAsync(data, offset, count-offset); if(n==0) throw new EndOfStreamException(); offset += n; }
        return data;
    }
    static async Task<byte[]> Block(NetworkStream s) => await Read(s, BitConverter.ToInt32(await Read(s,4),0));
    static async Task Reply(NetworkStream s, string value)
    {
        byte[] b = Encoding.UTF8.GetBytes(value);
        await s.WriteAsync(BitConverter.GetBytes(b.Length),0,4); await s.WriteAsync(b,0,b.Length);
    }
    static byte[] Inflate(byte[] packed)
    {
        using var input = new MemoryStream(packed);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream(); gzip.CopyTo(output); return output.ToArray();
    }
    static async Task Fault(string mode)
    {
        var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var server = Task.Run(async () => {
            using var peer = await listener.AcceptTcpClientAsync();
            var stream = peer.GetStream(); await Block(stream);
            try {
                if (mode == "oversize") { await stream.WriteAsync(BitConverter.GetBytes(65537),0,4); return; }
                if (mode == "disconnect") return;
                if (mode == "partial" || mode == "trickle") await stream.WriteAsync(BitConverter.GetBytes(100),0,4);
                if (mode == "trickle")
                    for(int n=0;n<15;n++) { await stream.WriteAsync(new byte[]{65},0,1); await Task.Delay(40); }
                else await Task.Delay(650);
            } catch (IOException) { } catch (ObjectDisposedException) { }
        });
        using var transport = new PoseTransport();
        var started = System.Diagnostics.Stopwatch.StartNew();
        byte[] left = null, right = null;
        if (mode == "blocked-write")
        {
            left = new byte[640*480*4]; right = new byte[640*480];
            new Random(42).NextBytes(left); new Random(43).NextBytes(right);
        }
        var pending = transport.ExchangeAsync("127.0.0.1",port,Encoding.UTF8.GetBytes("{}"),left,right,250);
        int ticks = 0;
        while (!pending.IsCompleted) { await Task.Delay(10); ticks++; if(mode=="cancel" && ticks==3) transport.Dispose(); }
        bool failed = false;
        try { await pending; } catch { failed = true; }
        Require(failed && started.ElapsedMilliseconds < 1200, mode+" exceeded whole-request bound");
        if(mode != "disconnect" && mode != "oversize") Require(ticks >= 2, "Caller did not remain responsive");
        await server; listener.Stop(); Console.WriteLine("PASS " + mode);
    }
    static async Task Main()
    {
        foreach(var mode in new[]{"silent","partial","trickle","cancel","disconnect","oversize","blocked-write"}) await Fault(mode);
        var retry = new PoseRetryPolicy(); double now = 0;
        foreach(int delay in new[]{1,2,4,8,8}) { retry.Fail(now); Require(!retry.CanAttempt(now+delay-.01) && retry.CanAttempt(now+delay),"Backoff interval wrong"); now += delay; }
        Require(retry.CircuitOpen,"Circuit did not open"); retry.Reset(); Require(!retry.CircuitOpen && retry.CanAttempt(0),"Retry reset failed");
        Console.WriteLine("PASS backoff/circuit/reset");
        var listener = new TcpListener(IPAddress.Loopback,0); listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        using var transport = new PoseTransport(); bool refused=false;
        try { await transport.ExchangeAsync("127.0.0.1",port,new byte[]{123,125},null,null,250); } catch { refused=true; }
        Require(refused,"Absent server unexpectedly accepted request");
        listener = new TcpListener(IPAddress.Loopback, port); listener.Start();
        var server = Task.Run(async () => {
            using var peer = await listener.AcceptTcpClientAsync(); var stream = peer.GetStream();
            await Block(stream); await Reply(stream,"health-ok");
            await Block(stream);
            byte[] l = Inflate(await Block(stream)), r = Inflate(await Block(stream));
            Require(l.Length==640*480*4 && l[123]==71 && r.Length==640*480 && r[456]==93,"Compression changed pixels");
            await Reply(stream,"frame-ok");
        });
        Require(await transport.ExchangeAsync("127.0.0.1",port,new byte[]{123,125},null,null,1000)=="health-ok","Recovery failed");
        var left = new byte[640*480*4]; left[123]=71; var right=new byte[640*480]; right[456]=93;
        Require(await transport.ExchangeAsync("127.0.0.1",port,new byte[]{123,125},left,right,1000)=="frame-ok","Frame failed");
        await server; listener.Stop();
        Console.WriteLine("PASS refused/recovered/persistent connection/lossless images");
        Console.WriteLine("POSE_TRANSPORT_CHECKS_PASS");
    }
}
