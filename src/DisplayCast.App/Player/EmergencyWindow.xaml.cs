using System.Windows;
using System.Windows.Threading;

namespace DisplayCast.Player;

/// <summary>紧急消息插播窗口：置顶显示，定时自动关闭。</summary>
public partial class EmergencyWindow : Window
{
    private DispatcherTimer? _timer;

    public EmergencyWindow(string? text, int seconds, double left, double top, double width, double height)
    {
        InitializeComponent();
        MessageText.Text = string.IsNullOrWhiteSpace(text) ? "紧急通知" : text.Trim();
        Left = left;
        Top = top;
        Width = width;
        Height = height;

        if (seconds > 0)
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            _timer.Tick += (_, _) => Close();
            _timer.Start();
        }
        Topmost = true;
    }
}
