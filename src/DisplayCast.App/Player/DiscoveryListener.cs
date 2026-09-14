using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DisplayCast.Net;

namespace DisplayCast.Player;

/// <summary>播放端 UDP 发现服务：响应 DISCOVER，并周期性广播公告到控制台。</summary>
public class DiscoveryListener
{
    private readonly PlayerWindow _window;
    private UdpClient? _udp;
    private readonly CancellationTokenSource _cts = new();

    public DiscoveryListener(PlayerWindow window)
    {
        _window = window;
    }

    public void Start()
    {
        try
        {
            var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            sock.Bind(new IPEndPoint(IPAddress.Any, Protocol.DiscoverPort));
            _udp = new UdpClient { Client = sock, EnableBroadcast = true };
            _ = ListenLoopAsync();
            _ = AnnounceLoopAsync();
        }
        catch
        {
            // 端口占用，忽略
        }
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try { result = await _udp!.ReceiveAsync(_cts.Token); }
            catch { break; }
            if (Encoding.UTF8.GetString(result.Buffer).Trim() == MsgType.Discover)
            {
                await Task.Delay(Random.Shared.Next(0, 300));
                await BroadcastAsync();
            }
        }
    }

    private async Task AnnounceLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await BroadcastAsync();
            await Task.Delay(TimeSpan.FromSeconds(15));
        }
    }

    private async Task BroadcastAsync()
    {
        try
        {
            var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(BuildAnnounce()));
            var ep = new IPEndPoint(IPAddress.Broadcast, Protocol.AnnouncePort);
            await _udp!.SendAsync(bytes, ep);
        }
        catch
        {
            // 网络异常，忽略
        }
    }

    private NetMsg BuildAnnounce() => _window.Dispatcher.Invoke(() => new NetMsg
    {
        Type = MsgType.Announce,
        Id = _window.DeviceId,
        Name = _window.DeviceName,
        Ip = GetLocalIp(),
        CurrentItem = _window.DeviceCurrentItem,
        Volume = _window.Volume,
        ScreenOn = _window.ScreenOn,
        TcpPort = _window.TcpPort
    });

    private static string GetLocalIp()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            return host.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)?.ToString() ?? "127.0.0.1";
        }
        catch
        {
            return "127.0.0.1";
        }
    }

    public void Stop()
    {
        _cts.Cancel();
        try { _udp?.Dispose(); } catch { }
    }
}
