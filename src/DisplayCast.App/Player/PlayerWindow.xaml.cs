using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using DisplayCast.Core;
using DisplayCast.Models;
using DisplayCast.Net;

namespace DisplayCast.Player;

/// <summary>播放端主窗口：全屏无边框，负责播放引擎与网络服务生命周期。</summary>
public partial class PlayerWindow : Window
{
    private readonly AppSettings _settings;
    private readonly MediaStore _store = new();
    private PlaybackEngine _engine = null!;
    private PlayerServer? _server;
    private DiscoveryListener? _discovery;
    private DispatcherTimer? _previewTimer;
    private DispatcherTimer? _scheduleTimer;
    private byte[]? _lastPreview;
    private int _volume;
    private bool _screenOn = true;
    private bool _wasOffBySchedule;
    private DateTime _lastScheduleCheck = DateTime.MinValue;

    public PlayerWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        _volume = Math.Clamp(settings.Volume, 0, 100);
        VideoPlayer.Volume = _volume / 100.0;

        ApplyScreenBounds();

        _engine = new PlaybackEngine(ImagePlayer, VideoPlayer, WebPlayer, Placeholder);
        _engine.CurrentItemChanged += name => DeviceCurrentItem = name;

        // 载入本地缓存的节目单（离线续播）
        var playlist = _store.LoadPlaylist();
        if (playlist != null)
            _engine.SetPlaylist(_store.Normalize(playlist));
    }

    // ---------- 供服务层读取的状态 ----------
    public string DeviceId => _settings.DeviceId;
    public string DeviceName => _settings.DeviceName;
    public string? DeviceCurrentItem { get; private set; }
    public int Volume => _volume;
    public bool ScreenOn => _screenOn;
    public int PreviewIntervalSeconds => _settings.PreviewIntervalSeconds;
    public int TcpPort => _settings.TcpPort;

    private void ApplyScreenBounds()
    {
        var screens = System.Windows.Forms.Screen.AllScreens;
        var idx = Math.Clamp(_settings.TargetScreen, 0, screens.Length - 1);
        var s = screens[idx];
        Left = s.Bounds.Left;
        Top = s.Bounds.Top;
        Width = s.Bounds.Width;
        Height = s.Bounds.Height;
        WindowState = WindowState.Normal;
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        _engine.Start();
        StartNetwork();
        StartScheduleTimer();

        // 预初始化 WebView2（网页播放依赖；失败则网页项显示占位）
        try { await WebPlayer.EnsureCoreWebView2Async(); }
        catch { }
    }

    private void StartNetwork()
    {
        _server = new PlayerServer(_settings.TcpPort, this);
        _server.Start();

        _discovery = new DiscoveryListener(this);
        _discovery.Start();

        if (_settings.PreviewIntervalSeconds > 0)
        {
            _previewTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_settings.PreviewIntervalSeconds) };
            _previewTimer.Tick += (_, _) => CapturePreview();
            _previewTimer.Start();
        }
    }

    private void StartScheduleTimer()
    {
        if (string.IsNullOrWhiteSpace(_settings.ScreenOnTime) && string.IsNullOrWhiteSpace(_settings.ScreenOffTime))
            return;
        _scheduleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _scheduleTimer.Tick += (_, _) => ApplySchedule();
        _scheduleTimer.Start();
        ApplySchedule();
    }

    private void ApplySchedule()
    {
        var now = DateTime.Now.TimeOfDay;
        if (TryParseTime(_settings.ScreenOffTime, out var off))
        {
            var onParsed = TryParseTime(_settings.ScreenOnTime, out var on);
            // 当天是否处于「关屏时段」
            bool inOff;
            if (onParsed && off <= on)
                inOff = now >= off && now < on;
            else if (onParsed)
                inOff = now >= off || now < on; // 跨午夜
            else
                inOff = now >= off; // 只关不自动开（仅当刚跨过关屏点时触发一次）

            if (inOff && !_wasOffBySchedule)
            {
                SetScreen(false);
                _wasOffBySchedule = true;
            }
            else if (!inOff && _wasOffBySchedule)
            {
                SetScreen(true);
                _wasOffBySchedule = false;
            }
        }
    }

    private static bool TryParseTime(string? hhmm, out TimeSpan t)
    {
        t = default;
        if (string.IsNullOrWhiteSpace(hhmm)) return false;
        return TimeSpan.TryParse(hhmm.Trim(), out t);
    }

    // ---------- 实时预览 ----------

    private void CapturePreview()
    {
        try
        {
            _lastPreview = ScreenCapture.CaptureJpeg(this);
        }
        catch
        {
            _lastPreview = null;
        }
    }

    public byte[]? GetPreview() => _lastPreview;

    /// <summary>立即抓取一帧并返回（远程操作网页时用于获得实时画面）。</summary>
    public byte[]? CaptureAndGetPreview()
    {
        CapturePreview();
        return _lastPreview;
    }

    // ---------- 供 PlayerServer 调用的操作（UI 线程） ----------

    public void ApplyPlaylist(Playlist playlist)
    {
        foreach (var it in playlist.Items)
        {
            if (it.Type != MediaType.Web && !string.IsNullOrEmpty(it.FilePath))
                it.FilePath = Path.Combine(AppPaths.MediaDir, Path.GetFileName(it.FilePath));
        }
        _store.SavePlaylist(playlist);
        _engine.SetPlaylist(playlist);
    }

    public string? SaveUploadedFile(string fileName, byte[] data) => _store.SaveFile(fileName, data);

    public List<string> ListFiles() => _store.ListFiles();

    public void RemoteNext() => _engine.Next();

    public void RemotePrev() => _engine.Prev();

    public void SetScreen(bool on)
    {
        _screenOn = on;
        ScreenOffOverlay.Visibility = on ? Visibility.Collapsed : Visibility.Visible;
    }

    public void SetVolume(int value)
    {
        _volume = Math.Clamp(value, 0, 100);
        VideoPlayer.Volume = _volume / 100.0;
    }

    public void RemoteRestart()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(path))
        {
            try { Process.Start(path); } catch { }
        }
        Application.Current.Shutdown();
    }

    public void ShowEmergency(string? text, int seconds)
    {
        var win = new EmergencyWindow(text, seconds, Left, Top, Width, Height);
        win.Show();
    }

    /// <summary>处理控制端发来的网页远程操作。</summary>
    public void HandleWebAction(string? action, double x, double y, string? text)
    {
        switch (action)
        {
            case "click": _engine.WebClick(x, y); break;
            case "input": _engine.WebInput(text ?? ""); break;
            case "key": _engine.WebKey(text ?? "Enter"); break;
            case "reload": _engine.WebReload(); break;
            case "back": _engine.WebBack(); break;
            case "forward": _engine.WebForward(); break;
            case "js": _engine.WebJs(text ?? ""); break;
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _engine.Dispose();
        _server?.Stop();
        _discovery?.Stop();
    }
}
