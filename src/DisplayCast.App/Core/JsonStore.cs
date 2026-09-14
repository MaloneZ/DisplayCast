using System.Text.Json;

namespace DisplayCast.Core;

/// <summary>通用 JSON 文件存储。</summary>
public static class JsonStore
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static T? Load<T>(string path) where T : class
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options);
        }
        catch
        {
            // 文件损坏时回退默认
        }
        return null;
    }

    public static void Save<T>(string path, T obj)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(obj, Options));
    }

    /// <summary>深拷贝（用于推送前把本地路径替换为文件名的副本）。</summary>
    public static T Clone<T>(T obj) where T : class
    {
        var json = JsonSerializer.Serialize(obj, Options);
        return JsonSerializer.Deserialize<T>(json, Options)!;
    }
}
