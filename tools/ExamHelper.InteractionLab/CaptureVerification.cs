using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ExamHelper.InteractionLab;

// Integration fixture captures only its own reference surface; never uploads pixels.
internal static class CaptureVerification
{
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var reference = new Window { Title = "ExamHelper Capture Reference", Left = 80, Top = 80, Width = 900, Height = 760,
            WindowStyle = WindowStyle.None, Background = new SolidColorBrush(Color.FromRgb(34, 102, 68)), ShowInTaskbar = false };
        reference.Show();
        await Task.Delay(400);
        var origin = reference.PointToScreen(new Point(0, 0));
        var scale = VisualTreeHelper.GetDpi(reference);
        var before = Capture((int)origin.X, (int)origin.Y, (int)(900 * scale.DpiScaleX), (int)(760 * scale.DpiScaleY));
        Save(before, Path.Combine(directory, "reference.png"));
        var overlay = new MainWindow { Left = 120, Top = 100, Width = 640, Height = 600 };
        if (!overlay.Protection.Success) throw new InvalidOperationException($"Protection failed: {overlay.Protection}");
        overlay.Show();
        await Task.Delay(500);
        var after = Capture((int)origin.X, (int)origin.Y, before.PixelWidth, before.PixelHeight);
        Save(after, Path.Combine(directory, "protected-capture.png"));
        var a = new byte[before.PixelWidth * before.PixelHeight * 4];
        var b = new byte[a.Length];
        before.CopyPixels(a, before.PixelWidth * 4, 0); after.CopyPixels(b, after.PixelWidth * 4, 0);
        int differences = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) differences++;
        File.WriteAllText(Path.Combine(directory, "capture-result.json"), JsonSerializer.Serialize(new {
            OS = Environment.OSVersion.VersionString, Protection = overlay.Protection,
            CaptureBackend = "GDI BitBlt SRCCOPY; own reference surface only", before.PixelWidth, before.PixelHeight,
            Differences = differences, Passed = differences == 0, Dpi = scale.PixelsPerInchX
        }, new JsonSerializerOptions { WriteIndented = true }));
        overlay.Close(); reference.Close();
        if (differences != 0) throw new InvalidOperationException($"Capture contains {differences} changed bytes; inspect artifacts.");
    }
    private static BitmapSource Capture(int x, int y, int width, int height)
    {
        var dc = GetDC(0); var mem = CreateCompatibleDC(dc); var bitmap = CreateCompatibleBitmap(dc, width, height);
        var old = SelectObject(mem, bitmap);
        try
        {
            if (!BitBlt(mem, 0, 0, width, height, dc, x, y, 0x00CC0020)) throw new InvalidOperationException("BitBlt failed");
            var source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0); converted.Freeze(); return converted;
        }
        finally { SelectObject(mem, old); DeleteObject(bitmap); DeleteDC(mem); ReleaseDC(0, dc); }
    }
    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(path); encoder.Save(stream);
    }
    [DllImport("user32.dll")] private static extern nint GetDC(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int w, int h);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
}
