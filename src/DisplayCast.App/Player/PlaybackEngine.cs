using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DisplayCast.Models;
using Microsoft.Web.WebView2.Wpf;

namespace DisplayCast.Player;

/// <summary>
/// 播放引擎：在图片 / 视频 / 网页三种控件间切换，实现节目单混合轮播。
/// 全部操作须在 UI 线程调用（PlayerWindow 内部已通过 Dispatcher 收敛）。
/// </summary>
public class PlaybackEngine : IDisposable
{
    private readonly Image _image;
    private readonly MediaElement _video;
    private readonly WebView2 _web;
    private readonly TextBlock _placeholder;

    private Playlist? _playlist;
    private readonly List<MediaItem> _playOrder = new();
    private int _index = -1;
    private readonly Random _rng = new();
    private TaskCompletionSource<bool>? _stepDone;
    private TaskCompletionSource<bool>? _videoEnded;
    private Task? _loop;
    private bool _disposed;

    /// <summary>当前播放条目名称变化时触发（用于回传状态）。</summary>
    public event Action<string?>? CurrentItemChanged;

    public PlaybackEngine(Image image, MediaElement video, WebView2 web, TextBlock placeholder)
    {
        _image = image;
        _video = video;
        _web = web;
        _placeholder = placeholder;
        _video.MediaEnded += OnVideoMediaEnded;
        _video.MediaFailed += (_, _) => _videoEnded?.TrySetResult(true);
    }

    private void OnVideoMediaEnded(object sender, RoutedEventArgs e) => _videoEnded?.TrySetResult(true);

    public void Start()
    {
        if (_loop != null) return;
        _loop = RunLoopAsync();
    }

    public void Stop() => Dispose();

    public void SetPlaylist(Playlist? playlist)
    {
        _playlist = playlist;
        RebuildOrder();
        _stepDone?.TrySetResult(true);
    }

    public void Next()
    {
        // 当前正在播的条目已预推进，直接结束当前即可进入下一个
        _stepDone?.TrySetResult(true);
    }

    public void Prev()
    {
        if (_playOrder.Count > 0)
        {
            _index = (_index - 2 + _playOrder.Count * 2) % _playOrder.Count;
        }
        _stepDone?.TrySetResult(true);
    }

    private void RebuildOrder()
    {
        _playOrder.Clear();
        if (_playlist == null) return;
        _playOrder.AddRange(_playlist.Items);
        if (_playlist.Order == PlayOrder.Random && _playOrder.Count > 1)
        {
            for (int i = _playOrder.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_playOrder[i], _playOrder[j]) = (_playOrder[j], _playOrder[i]);
            }
        }
        _index = _playOrder.Count > 0 ? 0 : -1;
    }

    private MediaItem? GetNextItem()
    {
        if (_playOrder.Count == 0) return null;
        if (_index < 0) _index = 0;
        _index %= _playOrder.Count;
        var item = _playOrder[_index];
        _index = (_index + 1) % _playOrder.Count;
        return item;
    }

    private async Task RunLoopAsync()
    {
        while (!_disposed)
        {
            var item = GetNextItem();
            if (item == null)
            {
                ShowPlaceholder("等待控制端下发节目…");
                _stepDone = NewStep();
                await _stepDone.Task;
                continue;
            }

            CurrentItemChanged?.Invoke(DisplayName(item));
            _stepDone = NewStep();
            await PlayItemAsync(item, _stepDone);
        }
    }

    private static TaskCompletionSource<bool> NewStep() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task PlayItemAsync(MediaItem item, TaskCompletionSource<bool> done)
    {
        switch (item.Type)
        {
            case MediaType.Image:
                ShowImage(item);
                _ = DelayThen(done, item.DurationSeconds > 0 ? item.DurationSeconds : 15);
                await done.Task;
                break;

            case MediaType.Video:
                _videoEnded = done;
                if (!ShowVideo(item))
                {
                    done.TrySetResult(true);
                    await done.Task;
                }
                else
                {
                    if (item.DurationSeconds > 0)
                        _ = DelayThen(done, item.DurationSeconds);
                    await done.Task;
                }
                // 清理视频
                _video.Stop();
                _video.Source = null;
                _videoEnded = null;
                break;

            case MediaType.Web:
                ShowWeb(item);
                _ = DelayThen(done, item.DurationSeconds > 0 ? item.DurationSeconds : 30);
                await done.Task;
                break;
        }
    }

    private async Task DelayThen(TaskCompletionSource<bool> done, int seconds)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(seconds));
        }
        catch
        {
            // 忽略
        }
        done.TrySetResult(true);
    }

    // ---------- 显示切换 ----------

    private void HideAll()
    {
        _image.Visibility = Visibility.Collapsed;
        _video.Visibility = Visibility.Collapsed;
        _web.Visibility = Visibility.Collapsed;
        _placeholder.Visibility = Visibility.Collapsed;
    }

    private void ShowPlaceholder(string text)
    {
        HideAll();
        _placeholder.Text = text;
        _placeholder.Visibility = Visibility.Visible;
    }

    private void ShowImage(MediaItem item)
    {
        if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
        {
            ShowPlaceholder($"素材缺失：{DisplayName(item)}");
            return;
        }
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.UriSource = new Uri(item.FilePath, UriKind.Absolute);
            bmp.EndInit();
            bmp.Freeze();
            _image.Source = bmp;
            HideAll();
            _image.Visibility = Visibility.Visible;
        }
        catch
        {
            ShowPlaceholder($"图片无法显示：{DisplayName(item)}");
        }
    }

    private bool ShowVideo(MediaItem item)
    {
        if (string.IsNullOrEmpty(item.FilePath) || !File.Exists(item.FilePath))
        {
            ShowPlaceholder($"素材缺失：{DisplayName(item)}");
            return false;
        }
        try
        {
            _video.Source = new Uri(item.FilePath, UriKind.Absolute);
            HideAll();
            _video.Visibility = Visibility.Visible;
            _video.Play();
            return true;
        }
        catch
        {
            ShowPlaceholder($"视频无法播放：{DisplayName(item)}");
            return false;
        }
    }

    private void ShowWeb(MediaItem item)
    {
        if (string.IsNullOrEmpty(item.Url))
        {
            ShowPlaceholder("网页地址为空");
            return;
        }
        try
        {
            _web.Source = new Uri(item.Url, UriKind.Absolute);
            HideAll();
            _web.Visibility = Visibility.Visible;
        }
        catch
        {
            // WebView2 尚未就绪
            ShowPlaceholder($"网页加载中：{item.Url}");
        }
    }

    private static string DisplayName(MediaItem item)
    {
        if (!string.IsNullOrEmpty(item.Name)) return item.Name;
        if (item.Type == MediaType.Web && !string.IsNullOrEmpty(item.Url)) return item.Url;
        if (!string.IsNullOrEmpty(item.FilePath)) return Path.GetFileName(item.FilePath);
        return "未命名素材";
    }

    // ================= 网页远程操作（控制端通过 JS 注入实现交互） =================

    /// <summary>是否正在播放网页（用于控制端判断可交互）。</summary>
    public bool IsWebVisible => _web.Visibility == Visibility.Visible;

    public void WebClick(double rx, double ry)
    {
        if (_web.CoreWebView2 == null) return;
        var x = Math.Clamp(rx, 0, 1);
        var y = Math.Clamp(ry, 0, 1);
        var script =
            "(function(){var x=innerWidth*" + x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",y=innerHeight*" + y.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ";var el=document.elementFromPoint(x,y);if(!el)return;" +
            "function f(t){el.dispatchEvent(new MouseEvent(t,{bubbles:true,cancelable:true,view:window,clientX:x,clientY:y,button:0}));}" +
            "f('mousedown');f('mouseup');f('click');})()";
        _ = _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    public void WebInput(string text)
    {
        if (_web.CoreWebView2 == null) return;
        var t = JsonSerializer.Serialize(text);
        var script =
            "(function(){var el=document.activeElement;if(!el)return;var t=" + t + ";" +
            "if(el.tagName==='INPUT'||el.tagName==='TEXTAREA'){" +
            "var p=el.tagName==='INPUT'?HTMLInputElement.prototype:HTMLTextAreaElement.prototype;" +
            "Object.getOwnPropertyDescriptor(p,'value').set.call(el,t);" +
            "el.dispatchEvent(new Event('input',{bubbles:true}));el.dispatchEvent(new Event('change',{bubbles:true}));" +
            "}else if(el.isContentEditable){el.textContent=t;el.dispatchEvent(new Event('input',{bubbles:true}));}})()";
        _ = _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    public void WebKey(string key)
    {
        if (_web.CoreWebView2 == null) return;
        var k = JsonSerializer.Serialize(key);
        var script =
            "(function(){var el=document.activeElement;if(!el)return;var k=" + k + ";" +
            "['keydown','keypress','keyup'].forEach(function(ty){" +
            "el.dispatchEvent(new KeyboardEvent(ty,{key:k,code:k,bubbles:true,cancelable:true}));});" +
            "if(k==='Enter'){var f=el.closest?el.closest('form'):null;" +
            "if(f){try{f.requestSubmit?f.requestSubmit():f.submit();}catch(e){}}}})()";
        _ = _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    public void WebReload()
    {
        _web.CoreWebView2?.Reload();
    }

    public void WebBack()
    {
        if (_web.CoreWebView2?.CanGoBack == true) _web.CoreWebView2.GoBack();
    }

    public void WebForward()
    {
        if (_web.CoreWebView2?.CanGoForward == true) _web.CoreWebView2.GoForward();
    }

    public void WebJs(string script)
    {
        if (_web.CoreWebView2 == null || string.IsNullOrWhiteSpace(script)) return;
        _ = _web.CoreWebView2.ExecuteScriptAsync(script);
    }

    public void Dispose()
    {
        _disposed = true;
        _stepDone?.TrySetResult(true);
        _videoEnded?.TrySetResult(true);
        _video.MediaEnded -= OnVideoMediaEnded;
    }
}
