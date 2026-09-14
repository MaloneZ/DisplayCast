using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace DisplayCast.ConsoleUI;

/// <summary>控制台设备列表项视图模型（支持实时预览缩略图刷新）。</summary>
public class DeviceViewModel : INotifyPropertyChanged
{
    private string _status = "在线";
    private string? _currentItem;
    private BitmapImage? _preview;
    private bool _screenOn = true;
    private int _volume = 100;
    private string _remark = "";

    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Ip { get; set; } = "";
    public int TcpPort { get; set; } = 8790;
    public string? Group { get; set; }
    public DateTime LastSeen { get; set; } = DateTime.UtcNow;

    /// <summary>设备备注（控制端本地维护，用于识别对应播放屏）。</summary>
    public string Remark
    {
        get => _remark;
        set { if (_remark != value) { _remark = value; OnChanged(nameof(Remark)); } }
    }

    public string Status
    {
        get => _status;
        set { if (_status != value) { _status = value; OnChanged(nameof(Status)); } }
    }

    public string? CurrentItem
    {
        get => _currentItem ?? "—";
        set { if (_currentItem != value) { _currentItem = value; OnChanged(nameof(CurrentItem)); } }
    }

    public bool ScreenOn
    {
        get => _screenOn;
        set { if (_screenOn != value) { _screenOn = value; OnChanged(nameof(ScreenOn)); } }
    }

    public int Volume
    {
        get => _volume;
        set { if (_volume != value) { _volume = value; OnChanged(nameof(Volume)); } }
    }

    public BitmapImage? Preview
    {
        get => _preview;
        set { if (_preview != value) { _preview = value; OnChanged(nameof(Preview)); } }
    }

    public void SetPreview(byte[]? jpeg)
    {
        if (jpeg == null || jpeg.Length == 0) return;
        try
        {
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = new MemoryStream(jpeg);
            bmp.EndInit();
            bmp.Freeze();
            Preview = bmp;
        }
        catch
        {
            // 忽略坏图
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
