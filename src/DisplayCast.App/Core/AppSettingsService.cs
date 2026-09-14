using System.IO;
using System.Text.Json;
using DisplayCast.Models;

namespace DisplayCast.Core;

/// <summary>应用配置的读写（JSON 存储于 %APPDATA%\DisplayCast\settings.json）。</summary>
public class AppSettingsService
{
    private static readonly string SettingsPath = Path.Combine(AppPaths.DataDir, "settings.json");

    public AppSettings Load()
    {
        AppSettings? settings = null;
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                settings = JsonSerializer.Deserialize<AppSettings>(json, JsonStore.Options);
            }
        }
        catch
        {
            // 配置损坏时回退到默认值
        }

        settings ??= new AppSettings();

        // 确保设备 ID 稳定（首次运行时生成并持久化）
        if (string.IsNullOrWhiteSpace(settings.DeviceId))
        {
            settings.DeviceId = Guid.NewGuid().ToString("N");
            Save(settings);
        }
        return settings;
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        var json = JsonSerializer.Serialize(settings, JsonStore.Options);
        File.WriteAllText(SettingsPath, json);
    }
}
