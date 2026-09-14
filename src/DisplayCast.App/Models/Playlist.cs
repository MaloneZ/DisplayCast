namespace DisplayCast.Models;

/// <summary>播放顺序。</summary>
public enum PlayOrder
{
    Sequential = 0,
    Random = 1
}

/// <summary>节目单：一组按顺序播放的素材。</summary>
public class Playlist
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name { get; set; } = "未命名节目单";

    public PlayOrder Order { get; set; } = PlayOrder.Sequential;

    public List<MediaItem> Items { get; set; } = new();
}
