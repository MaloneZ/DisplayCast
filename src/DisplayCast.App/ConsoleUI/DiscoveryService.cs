using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DisplayCast.Net;

namespace DisplayCast.ConsoleUI;

/// <summary>控制台 UDP 发现服务：监听播放端公告，并广播 DISCOVER。</summary>
public class DiscoveryService : IDisposable
{
    private UdpClient? _listener;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>收到播放端公告时触发（后台线程）。</summary>
    public event Action<NetMsg>? DeviceAnnounced;

    public void Start()
    {
        try
        {
            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sock.Bind(new IPEndPoint(IPAddress.Any, Protocol.AnnouncePort));
            _listener = new UdpClient { Client = sock };
            _ = ListenLoopAsync();
        }
        catch
        {
            // 端口占用，忽略
        }
    }

    public async Task DiscoverAsync()
    {
        try
        {
            using var sender = new UdpClient { EnableBroadcast = true };
            var bytes = Encoding.UTF8.GetBytes(MsgType.Discover);
            var ep = new IPEndPoint(IPAddress.Broadcast, Protocol.DiscoverPort);
            await sender.SendAsync(bytes, ep);
        }
        catch
        {
            // 网络异常，忽略
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await _listener!.ReceiveAsync(_cts.Token); }
            catch { break; }
            try
            {
                var msg = JsonSerializer.Deserialize<NetMsg>(Encoding.UTF8.GetString(result.Buffer));
                if (msg?.Type == MsgType.Announce)
                    DeviceAnnounced?.Invoke(msg);
            }
            catch
            {
                // 忽略坏包
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        try { _listener?.Dispose(); } catch { }
    }
}
