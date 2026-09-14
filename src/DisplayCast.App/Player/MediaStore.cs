using DisplayCast.Core;
using DisplayCast.Models;

namespace DisplayCast.Player;

/// <summary>播放端本地素材与节目单存储（断网续播、离线缓存）。</summary>
public class MediaStore
{
    public Playlist? LoadPlaylist() => JsonStore.Load<Playlist>(AppPaths.PlaylistPath);

    public void SavePlaylist(Playlist? playlist)
    {
        if (playlist == null)
        {
            if (File.Exists(AppPaths.PlaylistPath)) File.Delete(AppPaths.PlaylistPath);
            return;
        }
        JsonStore.Save(AppPaths.PlaylistPath, playlist);
    }

    /// <summary>保存素材文件，返回本地完整路径。</summary>
    public string SaveFile(string fileName, byte[] data)
    {
        Directory.CreateDirectory(AppPaths.MediaDir);
        var safeName = Path.GetFileName(fileName);
        var path = Path.Combine(AppPaths.MediaDir, safeName);
        File.WriteAllBytes(path, data);
        return path;
    }

    /// <summary>列出本地缓存的所有素材文件名。</summary>
    public List<string> ListFiles()
    {
        if (!Directory.Exists(AppPaths.MediaDir)) return new List<string>();
        return Directory.GetFiles(AppPaths.MediaDir).Select(Path.GetFileName).Where(n => n != null).Cast<string>().ToList();
    }

    /// <summary>把节目单中的素材路径修正为本地缓存路径（若本地已存在）。</summary>
    public Playlist Normalize(Playlist playlist)
    {
        foreach (var item in playlist.Items)
        {
            if (item.Type != MediaType.Web && !string.IsNullOrEmpty(item.FilePath))
            {
                var local = Path.Combine(AppPaths.MediaDir, Path.GetFileName(item.FilePath));
                if (File.Exists(local)) item.FilePath = local;
            }
        }
        return playlist;
    }
}
