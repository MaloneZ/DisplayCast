using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DisplayCast.Core;

/// <summary>使用 WPF 渲染管线抓取视觉对象画面并编码为 JPEG（用于实时预览回传）。</summary>
public static class ScreenCapture
{
    /// <summary>抓取指定视觉对象，等比缩略到 maxWidth 以内，编码为 JPEG。</summary>
    public static byte[]? CaptureJpeg(Visual visual, int maxWidth = 640, long quality = 55L)
    {
        if (visual == null) return null;

        var bounds = VisualTreeHelper.GetDescendantBounds(visual);
        int w = (int)Math.Ceiling(bounds.Width);
        int h = (int)Math.Ceiling(bounds.Height);
        if (w <= 0 || h <= 0) return null;

        // 等比缩略
        if (w > maxWidth)
        {
            h = Math.Max(1, (int)(h * (maxWidth / (double)w)));
            w = maxWidth;
        }

        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);

        var encoder = new JpegBitmapEncoder { QualityLevel = (int)Math.Clamp(quality, 1, 100) };
        encoder.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
