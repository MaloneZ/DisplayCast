using System.Text.Json.Serialization;

namespace DisplayCast.Models;

/// <summary>素材类型。</summary>
public enum MediaType
{
    Image,
    Video,
    Web
}

/// <summary>节目单中的一条素材（图片 / 视频 / 网页）。</summary>
public class MediaItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public MediaType Type { get; set; }

    /// <summary>图片/视频的本地路径（网页素材为空）。</summary>
    public string? FilePath { get; set; }

    /// <summary>网页地址（网页素材使用）。</summary>
    public string? Url { get; set; }

    /// <summary>播放时长（秒），图片默认 15s，视频 0 = 播放到结束。</summary>
    public int DurationSeconds { get; set; } = 15;

    /// <summary>显示名称。</summary>
    public string? Name { get; set; }

    [JsonIgnore]
    public string TypeLabel => Type switch
    {
        MediaType.Image => "图片",
        MediaType.Video => "视频",
        MediaType.Web => "网页",
        _ => "?"
    };

    [JsonIgnore]
    public string DisplayName =>
        !string.IsNullOrEmpty(Name) ? Name! :
        Type == MediaType.Web ? (Url ?? "网页") :
        !string.IsNullOrEmpty(FilePath) ? Path.GetFileName(FilePath) : "未命名素材";
}
