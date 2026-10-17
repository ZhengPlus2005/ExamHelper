using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
namespace ExamHelper.Windows.Capture;

public sealed record PhysicalDisplay(int X, int Y, int Width, int Height);
public interface IScreenCaptureBackend
{
    IReadOnlyList<PhysicalDisplay> GetDisplays();
    CapturedImage Capture(PhysicalDisplay display);
}
public sealed class FullScreenCapture(IScreenCaptureBackend? backend = null) : IScreenCapture
{
    private readonly IScreenCaptureBackend _backend = backend ?? new GdiScreenCaptureBackend();
    public Task<CapturedImage> CaptureAsync(CancellationToken ct) => Task.Run(() =>
    {
        ct.ThrowIfCancellationRequested();
        var displays = _backend.GetDisplays();
        if (displays.Count != 1 || displays[0].Width <= 0 || displays[0].Height <= 0)
            throw new ServiceFailure(FailureCategory.InvalidPayload, "仅支持一个有效显示器，请检查显示设置。");
        var display = displays[0];
        var image = _backend.Capture(display);
        ct.ThrowIfCancellationRequested();
        if (image.Width != display.Width || image.Height != display.Height || image.PngBytes.Length == 0)
            throw new ServiceFailure(FailureCategory.InvalidPayload, "截图尺寸与显示器物理像素不符。");
        return image;
    }, ct);
}
public sealed class GdiScreenCaptureBackend : IScreenCaptureBackend
{
    public IReadOnlyList<PhysicalDisplay> GetDisplays()
    {
        var displays = new List<PhysicalDisplay>();
        MonitorEnum callback = (nint monitor, nint dc, ref NativeRect rect, nint data) =>
        {
            displays.Add(new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top)); return true;
        };
        if (!EnumDisplayMonitors(0, 0, callback, 0)) throw Failure("读取显示器失败。");
        return displays;
    }
    public CapturedImage Capture(PhysicalDisplay display)
    {
        nint dc = 0, memory = 0, bitmap = 0, old = 0;
        try
        {
            dc = GetDC(0); if (dc == 0) throw Failure("无法访问屏幕。");
            memory = CreateCompatibleDC(dc); if (memory == 0) throw Failure("无法创建截图画布。");
            bitmap = CreateCompatibleBitmap(dc, display.Width, display.Height); if (bitmap == 0) throw Failure("无法创建截图位图。");
            old = SelectObject(memory, bitmap); if (old == 0 || old == -1) throw Failure("无法选择截图位图。");
            if (!BitBlt(memory, 0, 0, display.Width, display.Height, dc, display.X, display.Y, 0x00CC0020))
                throw Failure("屏幕截图失败。");
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream(); encoder.Save(stream);
            return new(stream.ToArray(), source.PixelWidth, source.PixelHeight);
        }
        finally
        {
            if (old != 0 && old != -1) SelectObject(memory, old);
            if (bitmap != 0) DeleteObject(bitmap);
            if (memory != 0) DeleteDC(memory);
            if (dc != 0) ReleaseDC(0, dc);
        }
    }
    private static ServiceFailure Failure(string message) => new(FailureCategory.Transport, message, Marshal.GetLastWin32Error().ToString());
    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
    private delegate bool MonitorEnum(nint monitor, nint dc, ref NativeRect rect, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool EnumDisplayMonitors(nint dc, nint clip, MonitorEnum callback, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint CreateCompatibleBitmap(nint dc, int width, int height);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll", SetLastError = true)] private static extern bool BitBlt(nint target, int x, int y, int width, int height, nint source, int sx, int sy, uint rop);
}