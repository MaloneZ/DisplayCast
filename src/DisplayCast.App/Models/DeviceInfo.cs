namespace DisplayCast.Models;

/// <summary>控制台侧看到的播放端设备信息。</summary>
public class DeviceInfo
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "";

    public string IpAddress { get; set; } = "";

    /// <summary>在线状态：在线 / 离线。</summary>
    public string Status { get; set; } = "离线";

    /// <summary>当前正在播放的素材名称。</summary>
    public string? CurrentItem { get; set; }

    public DateTime LastSeen { get; set; } = DateTime.UtcNow;
}
