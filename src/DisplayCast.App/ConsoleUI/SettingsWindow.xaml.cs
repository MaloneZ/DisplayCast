using System.Windows;
using System.Windows.Controls;
using DisplayCast.Core;
using DisplayCast.Models;

namespace DisplayCast.ConsoleUI;

/// <summary>设置窗口：设备名称、开机自启，以及角色专属配置。</summary>
public partial class SettingsWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AppSettingsService _service;

    private static readonly int[] PreviewValues = { 0, 1, 2, 3, 5, 10 };
    private static readonly string[] PreviewLabels = { "关闭", "1 秒", "2 秒", "3 秒", "5 秒", "10 秒" };

    public SettingsWindow(AppSettings settings, AppSettingsService service)
    {
        InitializeComponent();
        _settings = settings;
        _service = service;

        DeviceNameBox.Text = settings.DeviceName;
        AutoStartBox.IsChecked = settings.AutoStartEnabled;

        // 角色专属面板
        if (settings.Role == AppRole.Player)
        {
            ConsolePanel.Visibility = Visibility.Collapsed;
            PreviewIntervalCombo.ItemsSource = PreviewLabels;
            PreviewIntervalCombo.SelectedIndex = IndexOfPreview(settings.PreviewIntervalSeconds);
            TargetScreenBox.Text = settings.TargetScreen.ToString();
            ScreenOffTimeBox.Text = settings.ScreenOffTime ?? "";
            ScreenOnTimeBox.Text = settings.ScreenOnTime ?? "";
        }
        else
        {
            PlayerPanel.Visibility = Visibility.Collapsed;
            PasswordBox.Password = settings.ControlPassword ?? "";
        }
    }

    private static int IndexOfPreview(int value)
    {
        for (int i = 0; i < PreviewValues.Length; i++)
            if (PreviewValues[i] == value) return i;
        return 2;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.DeviceName = string.IsNullOrWhiteSpace(DeviceNameBox.Text)
            ? Environment.MachineName
            : DeviceNameBox.Text.Trim();
        _settings.AutoStartEnabled = AutoStartBox.IsChecked == true;

        if (_settings.Role == AppRole.Player)
        {
            _settings.PreviewIntervalSeconds = PreviewValues[Math.Clamp(PreviewIntervalCombo.SelectedIndex, 0, PreviewValues.Length - 1)];
            if (int.TryParse(TargetScreenBox.Text.Trim(), out int screen) && screen >= 0)
                _settings.TargetScreen = screen;
            _settings.ScreenOffTime = NormalizeTime(ScreenOffTimeBox.Text);
            _settings.ScreenOnTime = NormalizeTime(ScreenOnTimeBox.Text);
        }
        else
        {
            _settings.ControlPassword = string.IsNullOrEmpty(PasswordBox.Password) ? null : PasswordBox.Password;
        }

        _service.Save(_settings);
        AutoStartHelper.SetAutoStart(_settings.AutoStartEnabled);
        DialogResult = true;
        Close();
    }

    private static string? NormalizeTime(string input)
    {
        var t = input?.Trim();
        if (string.IsNullOrWhiteSpace(t)) return null;
        return TimeSpan.TryParse(t, out _) ? t : null;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
