using DisplayCast.Net;

namespace DisplayCast.Models;

/// <summary>应用配置，持久化到 %APPDATA%\DisplayCast\settings.json。</summary>
public class AppSettings
{
    /// <summary>是否已完成首次配置。</summary>
    public bool IsConfigured { get; set; }

    /// <summary>设备角色。</summary>
    public AppRole Role { get; set; } = AppRole.Player;

    /// <summary>设备名称（默认取主机名）。</summary>
    public string DeviceName { get; set; } = Environment.MachineName;

    /// <summary>设备唯一 ID（用于控制台识别，自动生成并持久化）。</summary>
    public string DeviceId { get; set; } = "";

    /// <summary>是否开机自启（默认开启）。</summary>
    public bool AutoStartEnabled { get; set; } = true;

    /// <summary>控制台解锁密码（空 = 无需密码）。</summary>
    public string? ControlPassword { get; set; }

    // ---------- 播放端 ----------

    /// <summary>TCP 控制端口。</summary>
    public int TcpPort { get; set; } = Protocol.DefaultTcpPort;

    /// <summary>实时预览截图间隔（秒），0 = 关闭。</summary>
    public int PreviewIntervalSeconds { get; set; } = 3;

    /// <summary>显示在哪个屏幕（0 = 主屏）。</summary>
    public int TargetScreen { get; set; } = 0;

    /// <summary>音量（0-100）。</summary>
    public int Volume { get; set; } = 100;

    /// <summary>每日自动关屏时间 HH:mm（空 = 不自动关屏）。</summary>
    public string? ScreenOffTime { get; set; }

    /// <summary>每日自动开屏时间 HH:mm（空 = 不自动开屏）。</summary>
    public string? ScreenOnTime { get; set; }
}
