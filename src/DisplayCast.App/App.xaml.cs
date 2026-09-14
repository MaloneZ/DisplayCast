using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DisplayCast.ConsoleUI;
using DisplayCast.Core;
using DisplayCast.Models;
using DisplayCast.Player;
using DisplayCast.Setup;

namespace DisplayCast;

public partial class App : Application
{
    private AppSettingsService _settingsService = null!;
    private Mutex? _mutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

        _settingsService = new AppSettingsService();
        var settings = _settingsService.Load();

        // 首次运行：弹出角色引导
        if (!settings.IsConfigured)
        {
            var setup = new SetupWindow(_settingsService, settings);
            setup.ShowDialog();
            if (!settings.IsConfigured)
            {
                Shutdown();
                return;
            }
        }

        var role = ParseRoleFromArgs(e.Args) ?? settings.Role;

        // 角色级单实例互斥（允许同一台机器同时跑播放端与控制台，便于调试）
        string mutexName = role == AppRole.Player ? "DisplayCast.Player" : "DisplayCast.Console";
        _mutex = new Mutex(true, mutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("程序已在运行中（当前角色）。", "DisplayCast",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        AutoStartHelper.SetAutoStart(settings.AutoStartEnabled);

        Window mainWindow = role == AppRole.Player
            ? new PlayerWindow(settings)
            : new ConsoleWindow(settings, _settingsService);

        MainWindow = mainWindow;
        mainWindow.Show();
    }

    private void OnDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        e.Handled = true;
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex) LogError(ex);
        // 播放端崩溃自动重启（看门狗）
        try
        {
            var settings = _settingsService.Load();
            if (settings.Role == AppRole.Player)
                Restart();
        }
        catch
        {
            // 忽略
        }
    }

    private static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDir);
            File.AppendAllText(AppPaths.ErrorLogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n");
        }
        catch
        {
            // 忽略日志失败
        }
    }

    private static void Restart()
    {
        try
        {
            var path = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(path))
                Process.Start(path);
        }
        catch
        {
            // 忽略
        }
    }

    private static AppRole? ParseRoleFromArgs(string[] args)
    {
        foreach (var a in args)
        {
            if (a.Equals("--role=player", StringComparison.OrdinalIgnoreCase)) return AppRole.Player;
            if (a.Equals("--role=console", StringComparison.OrdinalIgnoreCase)) return AppRole.Console;
        }
        return null;
    }
}
