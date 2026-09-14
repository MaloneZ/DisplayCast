using DisplayCast.Core;
using DisplayCast.Models;

namespace DisplayCast.ConsoleUI;

/// <summary>控制台持久化数据：素材库、节目单、设备分组、设备备注。</summary>
public class ConsoleData
{
    public List<MediaItem> Library { get; set; } = new();
    public List<Playlist> Playlists { get; set; } = new();
    public string ActivePlaylistId { get; set; } = "";
    public Dictionary<string, string> Groups { get; set; } = new();
    /// <summary>设备备注：设备 Id -> 备注文本。</summary>
    public Dictionary<string, string> Remarks { get; set; } = new();
}

/// <summary>控制台数据的读写。</summary>
public class ConsoleStore
{
    private readonly string _path = AppPaths.ConsoleDataPath;

    public ConsoleData Load() => JsonStore.Load<ConsoleData>(_path) ?? new ConsoleData();

    public void Save(ConsoleData data) => JsonStore.Save(_path, data);
}
