using System.Windows;
using DisplayCast.Core;
using DisplayCast.Models;

namespace DisplayCast.Setup;

/// <summary>首次运行引导：选择角色、设备名称、是否开机自启。</summary>
public partial class SetupWindow : Window
{
    private readonly AppSettingsService _service;
    private readonly AppSettings _settings;

    public SetupWindow(AppSettingsService service, AppSettings settings)
    {
        InitializeComponent();
        _service = service;
        _settings = settings;
        DeviceNameBox.Text = settings.DeviceName;
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.Role = ConsoleRadio.IsChecked == true ? AppRole.Console : AppRole.Player;
        _settings.DeviceName = string.IsNullOrWhiteSpace(DeviceNameBox.Text)
            ? Environment.MachineName
            : DeviceNameBox.Text.Trim();
        _settings.AutoStartEnabled = AutoStartBox.IsChecked == true;
        _settings.IsConfigured = true;

        _service.Save(_settings);
        AutoStartHelper.SetAutoStart(_settings.AutoStartEnabled);

        DialogResult = true;
        Close();
    }
}
