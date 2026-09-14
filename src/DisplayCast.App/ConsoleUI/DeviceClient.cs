using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using DisplayCast.Models;
using DisplayCast.Net;

namespace DisplayCast.ConsoleUI;

/// <summary>控制台到播放端的 TCP 客户端。</summary>
public class DeviceClient
{
    private readonly string _ip;
    private readonly int _port;

    public DeviceClient(string ip, int port)
    {
        _ip = ip;
        _port = port;
    }

    private async Task<NetMsg> RequestAsync(NetMsg request, Func<Stream, Task>? extraSend = null)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_ip, _port);
        using var stream = client.GetStream();
        await Framing.WriteAsync(stream, JsonBytes(request));
        if (extraSend != null) await extraSend(stream);
        var respBytes = await Framing.ReadAsync(stream);
        try
        {
            return JsonSerializer.Deserialize<NetMsg>(Encoding.UTF8.GetString(respBytes))
                ?? new NetMsg { Ok = false, Error = "空响应" };
        }
        catch
        {
            return new NetMsg { Ok = false, Error = "响应解析失败" };
        }
    }

    public Task<NetMsg> GetStatusAsync() => RequestAsync(new NetMsg { Type = MsgType.GetStatus });

    public async Task<List<string>> ListFilesAsync()
    {
        var resp = await RequestAsync(new NetMsg { Type = MsgType.ListFiles });
        return resp.Files ?? new List<string>();
    }

    public Task<NetMsg> SetPlaylistAsync(Playlist playlist) =>
        RequestAsync(new NetMsg { Type = MsgType.SetPlaylist, Playlist = playlist });

    public Task<NetMsg> UploadFileAsync(string fileName, byte[] data) =>
        RequestAsync(new NetMsg { Type = MsgType.UploadFile, FileName = fileName, FileLength = data.Length },
            s => Framing.WriteAsync(s, data));

    public async Task<byte[]?> GetPreviewAsync()
    {
        using var client = new TcpClient();
        await client.ConnectAsync(_ip, _port);
        using var stream = client.GetStream();
        await Framing.WriteAsync(stream, JsonBytes(new NetMsg { Type = MsgType.GetPreview }));
        var headerBytes = await Framing.ReadAsync(stream);
        NetMsg? header;
        try { header = JsonSerializer.Deserialize<NetMsg>(Encoding.UTF8.GetString(headerBytes)); }
        catch { header = null; }
        if (header == null || !header.Ok || header.PreviewLength <= 0) return null;
        return await Framing.ReadAsync(stream);
    }

    public Task<NetMsg> ControlAsync(string action, int value = 0) =>
        RequestAsync(new NetMsg { Type = MsgType.Control, ControlAction = action, Value = value });

    public Task<NetMsg> EmergencyAsync(string text, int seconds) =>
        RequestAsync(new NetMsg { Type = MsgType.Emergency, EmergencyText = text, EmergencyDurationSeconds = seconds });

    public Task<NetMsg> WebActionAsync(string action, double x = 0, double y = 0, string? text = null) =>
        RequestAsync(new NetMsg { Type = MsgType.WebAction, WebAction = action, WebX = x, WebY = y, WebText = text });

    private static byte[] JsonBytes(NetMsg m) => Encoding.UTF8.GetBytes(JsonSerializer.Serialize(m));
}
