using DisplayCast.Models;

namespace DisplayCast.Net;

/// <summary>协议常量。</summary>
public static class Protocol
{
    public const int DefaultTcpPort = 8790;   // 控制命令（TCP）
    public const int DiscoverPort = 8791;     // 控制台广播 DISCOVER 的目标端口（播放端监听）
    public const int AnnouncePort = 8792;     // 播放端广播公告的目标端口（控制台监听）
}

/// <summary>消息类型。</summary>
public static class MsgType
{
    public const string Discover = "discover";
    public const string Announce = "announce";
    public const string GetStatus = "get_status";
    public const string Status = "status";
    public const string ListFiles = "list_files";
    public const string SetPlaylist = "set_playlist";
    public const string UploadFile = "upload_file";
    public const string GetPreview = "get_preview";
    public const string Preview = "preview";
    public const string Control = "control";
    public const string Emergency = "emergency";
    public const string WebAction = "web_action";
    public const string Ack = "ack";
}

/// <summary>协议消息（JSON 序列化，扁平结构便于扩展）。</summary>
public class NetMsg
{
    public string Type { get; set; } = "";
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Ip { get; set; }
    public bool Ok { get; set; } = true;
    public string? Error { get; set; }

    // 状态
    public string? CurrentItem { get; set; }
    public int Volume { get; set; } = 100;
    public bool ScreenOn { get; set; } = true;
    public int PreviewIntervalSeconds { get; set; }
    public int TcpPort { get; set; } = Protocol.DefaultTcpPort;

    // 节目单
    public Playlist? Playlist { get; set; }

    // 文件
    public string? FileName { get; set; }
    public int FileLength { get; set; }
    public List<string>? Files { get; set; }

    // 控制动作：next / prev / screen_on / screen_off / volume / restart
    public string? ControlAction { get; set; }
    public int Value { get; set; }

    // 紧急插播
    public string? EmergencyText { get; set; }
    public int EmergencyDurationSeconds { get; set; } = 10;

    // 网页远程操作：action = click / input / key / reload / back / forward / js
    public string? WebAction { get; set; }
    public double WebX { get; set; }      // click 用，相对坐标 0~1
    public double WebY { get; set; }
    public string? WebText { get; set; }  // input 文本 / key 键名 / js 脚本

    // 预览二进制帧长度
    public int PreviewLength { get; set; }
}
