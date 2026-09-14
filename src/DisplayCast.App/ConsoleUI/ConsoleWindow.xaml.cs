using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using DisplayCast.Core;
using DisplayCast.Models;
using DisplayCast.Net;
using Microsoft.VisualBasic;
using Microsoft.Win32;

namespace DisplayCast.ConsoleUI;

/// <summary>控制台主窗口：设备管理、素材库、节目单编排、一键下发、远程控制。</summary>
public partial class ConsoleWindow : Window
{
    private readonly AppSettings _settings;
    private readonly AppSettingsService _settingsService;
    private readonly ConsoleStore _store = new();
    private ConsoleData _data = new();
    private DiscoveryService? _discovery;
    private readonly ObservableCollection<DeviceViewModel> _devices = new();
    private ObservableCollection<MediaItem> _library = new();
    private ObservableCollection<MediaItem> _playlistItems = new();
    private Playlist? _currentPlaylist;
    private DispatcherTimer? _refreshTimer;
    private bool _refreshing;

    private static readonly string[] ImageExts = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };

    public ConsoleWindow(AppSettings settings, AppSettingsService settingsService)
    {
        InitializeComponent();
        _settings = settings;
        _settingsService = settingsService;

        _data = _store.Load();
        _library = new ObservableCollection<MediaItem>(_data.Library);
        LibraryList.ItemsSource = _library;
        DevicesList.ItemsSource = _devices;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 载入节目单
        LoadPlaylists();

        // 启动设备发现
        _discovery = new DiscoveryService();
        _discovery.DeviceAnnounced += OnDeviceAnnounced;
        _discovery.Start();
        _ = _discovery.DiscoverAsync();

        // 定时刷新设备状态与预览
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += RefreshTimer_Tick;
        _refreshTimer.Start();

        // 密码锁定
        if (!string.IsNullOrWhiteSpace(_settings.ControlPassword))
        {
            LockOverlay.Visibility = Visibility.Visible;
            LockPasswordBox.Focus();
        }
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SaveData();
        _discovery?.Dispose();
        _refreshTimer?.Stop();
    }

    // ================= 节目单管理 =================

    private void LoadPlaylists()
    {
        if (_data.Playlists.Count == 0)
        {
            _data.Playlists.Add(new Playlist { Name = "默认节目单" });
        }
        if (string.IsNullOrEmpty(_data.ActivePlaylistId) ||
            _data.Playlists.All(p => p.Id != _data.ActivePlaylistId))
            _data.ActivePlaylistId = _data.Playlists[0].Id;

        PlaylistCombo.ItemsSource = _data.Playlists;
        PlaylistCombo.DisplayMemberPath = "Name";
        PlaylistCombo.SelectedItem = _data.Playlists.First(p => p.Id == _data.ActivePlaylistId);
    }

    private void PlaylistCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var p = PlaylistCombo.SelectedItem as Playlist;
        if (p == null) return;
        _currentPlaylist = p;
        _data.ActivePlaylistId = p.Id;
        _playlistItems = new ObservableCollection<MediaItem>(p.Items);
        PlaylistBox.ItemsSource = _playlistItems;
        OrderModeCombo.SelectedIndex = (int)p.Order;
    }

    private void OrderModeCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_currentPlaylist == null || OrderModeCombo.SelectedIndex < 0) return;
        _currentPlaylist.Order = (PlayOrder)OrderModeCombo.SelectedIndex;
        SaveData();
    }

    private void NewPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var p = new Playlist { Name = $"节目单 {_data.Playlists.Count + 1}" };
        _data.Playlists.Add(p);
        _data.ActivePlaylistId = p.Id;
        PlaylistCombo.ItemsSource = null;
        PlaylistCombo.ItemsSource = _data.Playlists;
        PlaylistCombo.SelectedItem = p;
        SaveData();
    }

    private void DeletePlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        if (_data.Playlists.Count <= 1)
        {
            UpdateStatusBar("至少保留一个节目单");
            return;
        }
        var p = PlaylistCombo.SelectedItem as Playlist;
        if (p == null) return;
        _data.Playlists.Remove(p);
        _data.ActivePlaylistId = _data.Playlists[0].Id;
        PlaylistCombo.ItemsSource = null;
        PlaylistCombo.ItemsSource = _data.Playlists;
        PlaylistCombo.SelectedItem = _data.Playlists[0];
        SaveData();
    }

    // ================= 素材库 =================

    private void AddMediaButton_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "媒体文件|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp;*.mp4;*.mkv;*.avi;*.mov;*.wmv|图片|*.jpg;*.jpeg;*.png;*.gif;*.bmp;*.webp|视频|*.mp4;*.mkv;*.avi;*.mov;*.wmv"
        };
        if (dlg.ShowDialog() != true) return;

        foreach (var f in dlg.FileNames)
        {
            var ext = Path.GetExtension(f).ToLowerInvariant();
            bool isImage = ImageExts.Contains(ext);
            _library.Add(new MediaItem
            {
                Type = isImage ? MediaType.Image : MediaType.Video,
                FilePath = f,
                Name = Path.GetFileName(f),
                DurationSeconds = isImage ? 15 : 0
            });
        }
        SaveData();
        UpdateStatusBar($"已导入 {dlg.FileNames.Length} 个素材");
    }

    private void AddWebButton_Click(object sender, RoutedEventArgs e)
    {
        var url = Interaction.InputBox("请输入网页地址（含 http:// 或 https://）：", "添加网页", "https://");
        if (string.IsNullOrWhiteSpace(url)) return;
        var name = Interaction.InputBox("请输入显示名称（可选）：", "网页名称", url.Trim());
        _library.Add(new MediaItem
        {
            Type = MediaType.Web,
            Url = url.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? url.Trim() : name.Trim(),
            DurationSeconds = 30
        });
        SaveData();
        UpdateStatusBar("已添加网页素材");
    }

    private void RemoveMediaButton_Click(object sender, RoutedEventArgs e)
    {
        var sel = LibraryList.SelectedItems.Cast<MediaItem>().ToList();
        foreach (var m in sel) _library.Remove(m);
        SaveData();
    }

    private void AddToPlaylistButton_Click(object sender, RoutedEventArgs e)
    {
        var sel = LibraryList.SelectedItems.Cast<MediaItem>().ToList();
        if (sel.Count == 0)
        {
            UpdateStatusBar("请先在素材库选中要加入的素材");
            return;
        }
        foreach (var m in sel) _playlistItems.Add(JsonStore.Clone(m));
        SaveData();
        UpdateStatusBar($"已加入 {sel.Count} 个素材到节目单");
    }

    private void LibraryList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (LibraryList.SelectedItem is MediaItem m) ShowPreview(m);
    }

    private void ShowPreview(MediaItem m)
    {
        PreviewVideo.Stop();
        PreviewVideo.Source = null;
        PreviewImage.Visibility = Visibility.Collapsed;
        PreviewVideo.Visibility = Visibility.Collapsed;
        PreviewHint.Visibility = Visibility.Visible;

        if (m.Type == MediaType.Image && !string.IsNullOrEmpty(m.FilePath) && File.Exists(m.FilePath))
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(m.FilePath, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                PreviewImage.Source = bmp;
                PreviewImage.Visibility = Visibility.Visible;
                PreviewHint.Visibility = Visibility.Collapsed;
            }
            catch { }
        }
        else if (m.Type == MediaType.Video && !string.IsNullOrEmpty(m.FilePath) && File.Exists(m.FilePath))
        {
            PreviewVideo.Source = new Uri(m.FilePath, UriKind.Absolute);
            PreviewVideo.Visibility = Visibility.Visible;
            PreviewVideo.Play();
            PreviewHint.Visibility = Visibility.Collapsed;
        }
        else if (m.Type == MediaType.Web)
        {
            PreviewHint.Text = $"网页素材：{m.Url}\n（网页在播放端全屏显示）";
        }
        else
        {
            PreviewHint.Text = "素材文件不存在";
        }
    }

    // ================= 节目单编辑 =================

    private void PlaylistUpButton_Click(object sender, RoutedEventArgs e) => MoveItem(-1);

    private void PlaylistDownButton_Click(object sender, RoutedEventArgs e) => MoveItem(1);

    private void MoveItem(int delta)
    {
        int i = PlaylistBox.SelectedIndex;
        if (i < 0) return;
        int j = i + delta;
        if (j < 0 || j >= _playlistItems.Count) return;
        (_playlistItems[i], _playlistItems[j]) = (_playlistItems[j], _playlistItems[i]);
        PlaylistBox.SelectedIndex = j;
        SaveData();
    }

    private void PlaylistRemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var sel = PlaylistBox.SelectedItems.Cast<MediaItem>().ToList();
        foreach (var m in sel) _playlistItems.Remove(m);
        SaveData();
    }

    private void PlaylistClearButton_Click(object sender, RoutedEventArgs e)
    {
        _playlistItems.Clear();
        SaveData();
    }

    private void ApplyDurationButton_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(DurationBox.Text.Trim(), out int sec) || sec < 0)
        {
            UpdateStatusBar("请输入合法的时长（秒，0=视频播完）");
            return;
        }
        var sel = PlaylistBox.SelectedItems.Cast<MediaItem>().ToList();
        if (sel.Count == 0) sel = _playlistItems.ToList();
        foreach (var m in sel) m.DurationSeconds = sec;
        PlaylistBox.Items.Refresh();
        SaveData();
    }

    // ================= 设备发现 =================

    private void OnDeviceAnnounced(NetMsg msg)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var id = msg.Id ?? "";
            var vm = _devices.FirstOrDefault(d => d.Id == id);
            if (vm == null)
            {
                vm = new DeviceViewModel { Id = id };
                _devices.Add(vm);
            }
            if (!string.IsNullOrEmpty(msg.Name)) vm.Name = msg.Name;
            if (!string.IsNullOrEmpty(msg.Ip)) vm.Ip = msg.Ip;
            if (msg.TcpPort > 0) vm.TcpPort = msg.TcpPort;
            vm.CurrentItem = msg.CurrentItem;
            vm.Volume = msg.Volume;
            vm.ScreenOn = msg.ScreenOn;
            vm.LastSeen = DateTime.UtcNow;
            vm.Status = "在线";
            if (_data.Groups.TryGetValue(id, out var g)) vm.Group = g;
            if (_data.Remarks.TryGetValue(id, out var r)) vm.Remark = r;
        });
    }

    private void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        _ = _discovery?.DiscoverAsync();
        UpdateStatusBar("正在扫描局域网设备…");
    }

    private async void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        if (_refreshing) return;
        _refreshing = true;
        try { await RefreshDevicesAsync(); }
        finally { _refreshing = false; }
    }

    private async Task RefreshDevicesAsync()
    {
        var online = _devices.Where(d => d.Status == "在线").ToList();
        var tasks = online.Select(async d =>
        {
            try
            {
                var client = new DeviceClient(d.Ip, d.TcpPort);
                var status = await client.GetStatusAsync();
                if (status.Ok)
                {
                    d.LastSeen = DateTime.UtcNow;
                    d.CurrentItem = status.CurrentItem;
                    d.Volume = status.Volume;
                    d.ScreenOn = status.ScreenOn;
                    var jpg = await client.GetPreviewAsync();
                    if (jpg != null) d.SetPreview(jpg);
                }
                else
                {
                    d.Status = "离线";
                }
            }
            catch
            {
                d.Status = "离线";
            }
        });
        await Task.WhenAll(tasks);

        var now = DateTime.UtcNow;
        foreach (var d in _devices)
        {
            if (d.Status == "在线" && (now - d.LastSeen).TotalSeconds > 45)
                d.Status = "离线";
        }
    }

    // ================= 下发 =================

    private async void PushButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = DevicesList.SelectedItems.Cast<DeviceViewModel>().ToList();
        if (selected.Count == 0)
        {
            UpdateStatusBar("请先在设备列表选择要下发的设备");
            return;
        }
        if (_currentPlaylist == null || _playlistItems.Count == 0)
        {
            UpdateStatusBar("节目单为空，请先添加素材");
            return;
        }

        PushButton.IsEnabled = false;
        try
        {
            var wire = JsonStore.Clone(_currentPlaylist);
            foreach (var it in wire.Items)
            {
                if (it.Type != MediaType.Web && !string.IsNullOrEmpty(it.FilePath))
                    it.FilePath = Path.GetFileName(it.FilePath);
            }

            foreach (var d in selected)
            {
                try
                {
                    var client = new DeviceClient(d.Ip, d.TcpPort);
                    var existing = await client.ListFilesAsync();
                    foreach (var it in _playlistItems)
                    {
                        if (it.Type == MediaType.Web || string.IsNullOrEmpty(it.FilePath)) continue;
                        var fn = Path.GetFileName(it.FilePath);
                        if (existing.Contains(fn)) continue;
                        if (!File.Exists(it.FilePath)) continue;
                        var data = await File.ReadAllBytesAsync(it.FilePath);
                        var up = await client.UploadFileAsync(fn, data);
                        if (!up.Ok) UpdateStatusBar($"{d.Name}：文件 {fn} 上传失败");
                    }
                    var set = await client.SetPlaylistAsync(wire);
                    UpdateStatusBar(set.Ok ? $"{d.Name}：下发成功" : $"{d.Name}：下发失败 {set.Error}");
                }
                catch (Exception ex)
                {
                    UpdateStatusBar($"{d.Name}：下发异常 {ex.Message}");
                }
            }
        }
        finally
        {
            PushButton.IsEnabled = true;
        }
    }

    // ================= 远程控制 =================

    private DeviceViewModel? SelectedDevice() => DevicesList.SelectedItem as DeviceViewModel;

    private async void DeviceNextButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d != null) await new DeviceClient(d.Ip, d.TcpPort).ControlAsync("next");
    }

    private async void DevicePrevButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d != null) await new DeviceClient(d.Ip, d.TcpPort).ControlAsync("prev");
    }

    private async void DeviceScreenToggleButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d == null) return;
        var r = await new DeviceClient(d.Ip, d.TcpPort).ControlAsync(d.ScreenOn ? "screen_off" : "screen_on");
        if (r.Ok) d.ScreenOn = !d.ScreenOn;
    }

    private async void VolumeDownButton_Click(object sender, RoutedEventArgs e) => await AdjustVolume(-10);

    private async void VolumeUpButton_Click(object sender, RoutedEventArgs e) => await AdjustVolume(10);

    private async Task AdjustVolume(int delta)
    {
        var d = SelectedDevice();
        if (d == null) return;
        var v = Math.Clamp(d.Volume + delta, 0, 100);
        var r = await new DeviceClient(d.Ip, d.TcpPort).ControlAsync("volume", v);
        if (r.Ok) d.Volume = v;
    }

    private async void DeviceRestartButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d == null) return;
        var r = MessageBox.Show($"确定要重启播放端「{d.Name}」吗？", "重启", MessageBoxButton.OKCancel, MessageBoxImage.Question);
        if (r == MessageBoxResult.OK)
            await new DeviceClient(d.Ip, d.TcpPort).ControlAsync("restart");
    }

    private async void EmergencyButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d == null)
        {
            UpdateStatusBar("请先选择一个设备");
            return;
        }
        var text = Interaction.InputBox("请输入要插播的消息内容：", "紧急插播", "");
        if (string.IsNullOrWhiteSpace(text)) return;
        await new DeviceClient(d.Ip, d.TcpPort).EmergencyAsync(text.Trim(), 15);
        UpdateStatusBar($"已向 {d.Name} 发送紧急消息");
    }

    // ================= 分组 =================

    private void ApplyGroupButton_Click(object sender, RoutedEventArgs e)
    {
        var group = GroupBox.Text.Trim();
        foreach (var d in DevicesList.SelectedItems.Cast<DeviceViewModel>())
        {
            d.Group = group;
            if (string.IsNullOrEmpty(group)) _data.Groups.Remove(d.Id);
            else _data.Groups[d.Id] = group;
        }
        SaveData();
    }

    // ================= 设备备注 =================

    private void DeviceRemarkButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d == null)
        {
            UpdateStatusBar("请先选择一个设备");
            return;
        }
        var remark = Interaction.InputBox(
            $"请输入设备「{d.Name}」的备注（用于识别对应播放屏，可留空清除）：",
            "编辑设备备注", d.Remark);
        if (remark == null) return; // 用户取消
        remark = remark.Trim();
        d.Remark = remark;
        if (string.IsNullOrEmpty(remark)) _data.Remarks.Remove(d.Id);
        else _data.Remarks[d.Id] = remark;
        SaveData();
        UpdateStatusBar($"已更新 {d.Name} 的备注");
    }

    private void DeviceRemoteButton_Click(object sender, RoutedEventArgs e)
    {
        var d = SelectedDevice();
        if (d == null)
        {
            UpdateStatusBar("请先选择一个设备");
            return;
        }
        if (d.Status != "在线")
        {
            UpdateStatusBar("设备离线，无法远程操作");
            return;
        }
        var win = new WebControlWindow(d);
        win.Owner = this;
        win.Show();
    }

    // ================= 设置 / 锁定 =================

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var win = new SettingsWindow(_settings, _settingsService);
        win.Owner = this;
        win.ShowDialog();

        // 设置变更后重新评估锁定
        if (!string.IsNullOrWhiteSpace(_settings.ControlPassword))
        {
            LockOverlay.Visibility = Visibility.Visible;
            LockPasswordBox.Clear();
        }
        else
        {
            LockOverlay.Visibility = Visibility.Collapsed;
        }
    }

    private void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        if (LockPasswordBox.Password == _settings.ControlPassword)
        {
            LockOverlay.Visibility = Visibility.Collapsed;
            LockHint.Text = "";
            LockPasswordBox.Clear();
        }
        else
        {
            LockHint.Text = "密码错误，请重试";
        }
    }

    private void LockPasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) UnlockButton_Click(sender, e);
    }

    // ================= 工具 =================

    private void SaveData()
    {
        _data.Library = _library.ToList();
        if (_currentPlaylist != null) _currentPlaylist.Items = _playlistItems.ToList();
        _store.Save(_data);
    }

    private void UpdateStatusBar(string text)
    {
        StatusText.Text = $"{DateTime.Now:HH:mm:ss}  {text}";
    }
}
