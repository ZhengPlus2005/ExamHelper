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
    public static async Task RunAsync(string directory, bool waitForReference = false)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "capture-result.json"), "{\"Passed\":false,\"Status\":\"Running\"}");
        if (File.Exists(Path.Combine(directory, "error.txt"))) File.Delete(Path.Combine(directory, "error.txt"));
        var reference = new Window { Title = "ExamHelper Capture Reference", Left = 80, Top = 80, Width = 900, Height = 760,
            WindowStyle = WindowStyle.None, Background = new SolidColorBrush(Color.FromRgb(34, 102, 68)), Topmost = true, ShowInTaskbar = true };
        reference.Show();
        await Task.Delay(400);
        if (!IsWindowVisible(new WindowInteropHelper(reference).Handle)) throw new InvalidOperationException("Reference HWND is hidden by process startup flags; capture test inconclusive.");
        var origin = reference.PointToScreen(new Point(0, 0));
        var scale = VisualTreeHelper.GetDpi(reference);
        const int inset = 20; // Exclude DWM rounded corners and resize frame.
        var before = Capture((int)origin.X + inset, (int)origin.Y + inset, (int)(900 * scale.DpiScaleX) - inset * 2, (int)(760 * scale.DpiScaleY) - inset * 2);
        // A background process may fail to activate its reference window. Do
        // not compare unrelated desktop pixels or save them as test evidence.
        var baselinePixels = new byte[before.PixelWidth * before.PixelHeight * 4];
        before.CopyPixels(baselinePixels, before.PixelWidth * 4, 0);
        for (var i = 0; i < baselinePixels.Length; i += 4)
            if (baselinePixels[i] != 68 || baselinePixels[i + 1] != 102 || baselinePixels[i + 2] != 34)
                throw new InvalidOperationException($"Reference surface not fully visible (first non-green BGR={baselinePixels[i]},{baselinePixels[i + 1]},{baselinePixels[i + 2]}); capture test inconclusive. No desktop pixels saved.");
        Save(before, Path.Combine(directory, "reference.png"));
        var overlay = new MainWindow(registerHotkeys: false) { Left = 120, Top = 100, Width = 640, Height = 600 };
        if (!overlay.Protection.Success) throw new InvalidOperationException($"Protection failed: {overlay.Protection}");
        overlay.Show();
        await Task.Delay(500);
        var after = Capture((int)origin.X + inset, (int)origin.Y + inset, before.PixelWidth, before.PixelHeight);
        Save(after, Path.Combine(directory, "protected-capture.png"));
        var a = new byte[before.PixelWidth * before.PixelHeight * 4];
        var b = new byte[a.Length];
        before.CopyPixels(a, before.PixelWidth * 4, 0); after.CopyPixels(b, after.PixelWidth * 4, 0);
        int differences = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) differences++;
        if (waitForReference)
        {
            overlay.Hide();
            reference.Content = new System.Windows.Controls.TextBlock { Text = "ExamHelper 原生验证参考窗：激活此窗口后继续焦点验证。", Foreground = Brushes.White, FontSize = 20, Margin = new Thickness(30) };
            var deadline = DateTime.UtcNow.AddSeconds(45);
            while (GetForegroundWindow() != new WindowInteropHelper(reference).Handle && DateTime.UtcNow < deadline) await Task.Delay(50);
            if (GetForegroundWindow() != new WindowInteropHelper(reference).Handle) throw new InvalidOperationException("Reference activation precondition not met; focus test inconclusive.");
            overlay.Show();
        }
        var nativeChecks = NativeVerification.Run(reference, overlay);
        File.WriteAllText(Path.Combine(directory, "capture-result.json"), JsonSerializer.Serialize(new {
            OS = Environment.OSVersion.VersionString, Protection = overlay.Protection,
            CaptureBackend = "GDI BitBlt SRCCOPY; own reference surface only", before.PixelWidth, before.PixelHeight,
            Differences = differences, CapturePassed = differences == 0, Passed = differences == 0 && nativeChecks.All(c => c.Value), NativeChecks = nativeChecks, NativeDiagnostics = NativeVerification.Diagnostics, Dpi = scale.PixelsPerInchX
        }, new JsonSerializerOptions { WriteIndented = true }));
        overlay.Close(); reference.Close();
        if (differences != 0) throw new InvalidOperationException($"Capture contains {differences} changed bytes; inspect artifacts.");
        if (nativeChecks.Any(c => !c.Value)) throw new InvalidOperationException("Native integration check failed; inspect capture-result.json.");
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
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll")] private static extern int ReleaseDC(nint hwnd, nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleBitmap(nint dc, int w, int h);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(nint obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(nint dest, int x, int y, int w, int h, nint src, int sx, int sy, uint rop);
}
