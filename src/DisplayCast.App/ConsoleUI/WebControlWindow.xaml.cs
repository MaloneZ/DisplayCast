using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.VisualBasic;

namespace DisplayCast.ConsoleUI;

/// <summary>远程操作窗口：实时显示播放端画面，支持点击/输入/导航控制播放端网页。</summary>
public partial class WebControlWindow : Window
{
    private readonly DeviceViewModel _device;
    private readonly DispatcherTimer _timer;
    private bool _refreshing;

    public WebControlWindow(DeviceViewModel device)
    {
        InitializeComponent();
        _device = device;
        TitleText.Text = $"远程操作 · {device.Name}" + (string.IsNullOrEmpty(device.Remark) ? "" : $"（{device.Remark}）");
        Title = $"远程操作网页 - {device.Name}";

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        _timer.Tick += Timer_Tick;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _timer.Start();
        _ = RefreshPreviewAsync();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _timer.Stop();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        if (_refreshing) return;
        _refreshing = true;
        try { await RefreshPreviewAsync(); }
        finally { _refreshing = false; }
    }

    private async Task RefreshPreviewAsync()
    {
        try
        {
            var jpg = await new DeviceClient(_device.Ip, _device.TcpPort).GetPreviewAsync();
            if (jpg != null && jpg.Length > 0) SetImage(jpg);
        }
        catch
        {
            LoadingText.Text = "连接播放端失败（设备可能离线）";
        }
    }

    private void SetImage(byte[] jpeg)
    {
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(jpeg);
            bmp.EndInit();
            bmp.Freeze();
            RemoteImage.Source = bmp;
            LoadingText.Visibility = Visibility.Collapsed;
        }
        catch
        {
            // 忽略坏图
        }
    }

    // ---------- 画面点击 → 相对坐标 → 播放端模拟点击 ----------

    private void RemoteImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (RemoteImage.Source is not BitmapSource bmp) return;
        var pos = e.GetPosition(RemoteImage);
        double ctrlW = RemoteImage.ActualWidth, ctrlH = RemoteImage.ActualHeight;
        double imgW = bmp.PixelWidth, imgH = bmp.PixelHeight;
        if (ctrlW <= 0 || ctrlH <= 0 || imgW <= 0 || imgH <= 0) return;

        // Stretch=Uniform：等比缩放居中，扣除留白
        double scale = Math.Min(ctrlW / imgW, ctrlH / imgH);
        double dispW = imgW * scale, dispH = imgH * scale;
        double ox = (ctrlW - dispW) / 2, oy = (ctrlH - dispH) / 2;
        double ix = pos.X - ox, iy = pos.Y - oy;
        if (ix < 0 || iy < 0 || ix > dispW || iy > dispH) return; // 点在留白区

        double rx = ix / dispW, ry = iy / dispH;
        _ = SendAsync("click", rx, ry);
    }

    // ---------- 操作按钮 ----------

    private void ReloadButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync("reload");

    private void BackButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync("back");

    private void ForwardButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync("forward");

    private void SendInputButton_Click(object sender, RoutedEventArgs e)
    {
        var text = InputBox.Text;
        if (string.IsNullOrEmpty(text)) return;
        _ = SendAsync("input", text: text);
        InputBox.Text = "";
    }

    private void EnterKeyButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync("key", text: "Enter");

    private void BackspaceButton_Click(object sender, RoutedEventArgs e) => _ = SendAsync("key", text: "Backspace");

    private void InputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendInputButton_Click(sender, e);
            e.Handled = true;
        }
    }

    private void JsButton_Click(object sender, RoutedEventArgs e)
    {
        var script = Interaction.InputBox("请输入要执行的 JavaScript 代码：", "执行 JS", "");
        if (string.IsNullOrWhiteSpace(script)) return;
        _ = SendAsync("js", text: script.Trim());
    }

    private async Task SendAsync(string action, double x = 0, double y = 0, string? text = null)
    {
        try
        {
            await new DeviceClient(_device.Ip, _device.TcpPort).WebActionAsync(action, x, y, text);
        }
        catch
        {
            LoadingText.Text = "发送失败（设备可能离线）";
            LoadingText.Visibility = Visibility.Visible;
        }
    }
}
