namespace DisplayCast.Core;

/// <summary>应用数据目录与文件路径。</summary>
public static class AppPaths
{
    public static string DataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DisplayCast");

    public static string MediaDir => Path.Combine(DataDir, "media");

    public static string PlaylistPath => Path.Combine(DataDir, "playlist.json");

    public static string ConsoleDataPath => Path.Combine(DataDir, "console.json");

    public static string ErrorLogPath => Path.Combine(DataDir, "error.log");
}
