using System.Runtime.InteropServices;

namespace DisplayCast.Core;

/// <summary>通过 Win32 API 枚举显示器（不依赖 WinForms）。</summary>
public static class ScreenHelper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ScreenRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;

        public int Width => Right - Left;
        public int Height => Bottom - Top;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

    /// <summary>返回所有显示器在虚拟屏幕坐标系中的矩形。</summary>
    public static ScreenRect[] GetScreens()
    {
        var list = new List<ScreenRect>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr lParam) =>
        {
            list.Add(new ScreenRect { Left = r.Left, Top = r.Top, Right = r.Right, Bottom = r.Bottom });
            return true;
        }, IntPtr.Zero);
        return list.ToArray();
    }
}
