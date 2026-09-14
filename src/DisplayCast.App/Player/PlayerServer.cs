using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DisplayCast.Net;

namespace DisplayCast.Player;

/// <summary>播放端 TCP 控制服务：接收控制台命令并返回结果。</summary>
public class PlayerServer
{
    private readonly int _port;
    private readonly PlayerWindow _window;
    private TcpListener? _listener;
    private readonly CancellationTokenSource _cts = new();

    public PlayerServer(int port, PlayerWindow window)
    {
        _port = port;
        _window = window;
    }

    public void Start()
    {
        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _ = AcceptLoopAsync();
        }
        catch
        {
            // 端口被占用等，忽略（不影响播放）
        }
    }

    private async Task AcceptLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await _listener!.AcceptTcpClientAsync(_cts.Token); }
            catch { break; }
            _ = HandleClientAsync(client);
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var payload = await Framing.ReadAsync(stream);
                    var msg = JsonSerializer.Deserialize<NetMsg>(Encoding.UTF8.GetString(payload));
                    if (msg == null) break;
                    await HandleMessageAsync(stream, msg);
                }
            }
            catch
            {
                // 连接断开或协议错误，结束本次会话
            }
        }
    }

    private async Task HandleMessageAsync(Stream stream, NetMsg msg)
    {
        switch (msg.Type)
        {
            case MsgType.GetStatus:
                await WriteAsync(stream, _window.Dispatcher.Invoke(() => new NetMsg
                {
                    Type = MsgType.Status,
                    Ok = true,
                    Id = _window.DeviceId,
                    Name = _window.DeviceName,
                    CurrentItem = _window.DeviceCurrentItem,
                    Volume = _window.Volume,
                    ScreenOn = _window.ScreenOn,
                    PreviewIntervalSeconds = _window.PreviewIntervalSeconds,
                    TcpPort = _window.TcpPort
                }));
                break;

            case MsgType.ListFiles:
                var files = _window.Dispatcher.Invoke(() => _window.ListFiles());
                await WriteAsync(stream, new NetMsg { Type = MsgType.Ack, Ok = true, Files = files });
                break;

            case MsgType.SetPlaylist:
                if (msg.Playlist != null)
                    _window.Dispatcher.Invoke(() => _window.ApplyPlaylist(msg.Playlist!));
                await WriteAsync(stream, Ack(true));
                break;

            case MsgType.UploadFile:
                {
                    var data = await Framing.ReadAsync(stream);
                    string? path = null;
                    if (!string.IsNullOrEmpty(msg.FileName))
                        path = _window.Dispatcher.Invoke(() => _window.SaveUploadedFile(msg.FileName!, data));
                    await WriteAsync(stream, Ack(path != null, path == null ? "保存失败" : null));
                }
                break;

            case MsgType.GetPreview:
                {
                    var jpg = _window.Dispatcher.Invoke(_window.CaptureAndGetPreview);
                    var header = new NetMsg { Type = MsgType.Preview, Ok = true, PreviewLength = jpg?.Length ?? 0 };
                    await WriteAsync(stream, header);
                    if (jpg != null) await Framing.WriteAsync(stream, jpg);
                }
                break;

            case MsgType.Control:
                {
                    bool ok = true;
                    string? err = null;
                    _window.Dispatcher.Invoke(() =>
                    {
                        switch (msg.ControlAction)
                        {
                            case "next": _window.RemoteNext(); break;
                            case "prev": _window.RemotePrev(); break;
                            case "screen_on": _window.SetScreen(true); break;
                            case "screen_off": _window.SetScreen(false); break;
                            case "volume": _window.SetVolume(msg.Value); break;
                            case "restart": _window.RemoteRestart(); break;
                            default: ok = false; err = "未知控制命令"; break;
                        }
                    });
                    await WriteAsync(stream, Ack(ok, err));
                }
                break;

            case MsgType.Emergency:
                _window.Dispatcher.Invoke(() => _window.ShowEmergency(msg.EmergencyText, msg.EmergencyDurationSeconds));
                await WriteAsync(stream, Ack(true));
                break;

            case MsgType.WebAction:
                _window.Dispatcher.Invoke(() => _window.HandleWebAction(msg.WebAction, msg.WebX, msg.WebY, msg.WebText));
                await WriteAsync(stream, Ack(true));
                break;

            default:
                await WriteAsync(stream, Ack(false, "未知命令"));
                break;
        }
    }

    private static NetMsg Ack(bool ok, string? err = null) => new() { Type = MsgType.Ack, Ok = ok, Error = err };

    private static byte[] JsonBytes(NetMsg m) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m));

    private static Task WriteAsync(Stream s, NetMsg m) => Framing.WriteAsync(s, JsonBytes(m));

    public void Stop()
    {
        _cts.Cancel();
        try { _listener?.Stop(); } catch { }
    }
}
