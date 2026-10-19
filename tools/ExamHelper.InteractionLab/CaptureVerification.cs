using ExamHelper.Windows.Capture;
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
    public static async Task RunAsync(string directory,bool productWindow = false,bool captureProtectionEnabled = true)
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
        await using var product = productWindow ? await ProductCaptureFixture.CreateAsync(captureProtectionEnabled:captureProtectionEnabled) : null;
        var lab = productWindow ? null : new MainWindow(registerHotkeys: false) { Left = 120, Top = 100, Width = 640, Height = 600 };
        Window overlay = product?.Window ?? (Window)lab!;
        var protection = product?.Window.Protection ?? lab!.Protection;
        if (!protection.Success || protection.EffectiveAffinity != (captureProtectionEnabled ? 17u : 0u)) throw new InvalidOperationException($"Protection failed: {protection}");
        overlay.Show();
        await Task.Delay(500);
        product?.PrepareProtectedSelectors();
        await Task.Delay(100);
        product?.SavePreview(directory);
        var after = Capture((int)origin.X + inset, (int)origin.Y + inset, before.PixelWidth, before.PixelHeight);

        var a = new byte[before.PixelWidth * before.PixelHeight * 4];
        var b = new byte[a.Length];
        before.CopyPixels(a, before.PixelWidth * 4, 0); after.CopyPixels(b, after.PixelWidth * 4, 0);
        int differences = 0; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) differences++;
        var capturePassed = captureProtectionEnabled ? differences == 0 : differences > 0;
        if(capturePassed) Save(after,Path.Combine(directory,captureProtectionEnabled ? "protected-capture.png" : "visible-capture.png"));
        var nativeChecks = NativeVerification.Run(reference, overlay);
        File.WriteAllText(Path.Combine(directory, "capture-result.json"), JsonSerializer.Serialize(new {
            OS = Environment.OSVersion.VersionString, Protection = protection, CaptureProtectionEnabled = captureProtectionEnabled,
            CaptureBackend = "GDI BitBlt SRCCOPY; own reference surface only", before.PixelWidth, before.PixelHeight,
            Differences = differences, CapturePassed = capturePassed, Passed = capturePassed && nativeChecks.All(c => c.Value), NativeChecks = nativeChecks, Dpi = scale.PixelsPerInchX
        }, new JsonSerializerOptions { WriteIndented = true }));
        if(product is not null) product.Window.CloseFromHost(); else overlay.Close(); reference.Close();
        if (!capturePassed) throw new InvalidOperationException($"Unexpected capture differences ({differences} bytes) for capture protection={captureProtectionEnabled}.");
        if (nativeChecks.Any(c => !c.Value)) throw new InvalidOperationException("Native integration check failed; inspect capture-result.json.");
    }
    private static BitmapSource Capture(int x, int y, int width, int height)
    {
        // Exercise the product backend while persisting only the known reference crop.
        var image = new GdiScreenCaptureBackend().Capture(new(x, y, width, height));
        using var stream = new MemoryStream(image.PngBytes);
        var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var converted = new FormatConvertedBitmap(decoder.Frames[0], PixelFormats.Bgra32, null, 0);
        converted.Freeze();
        return converted;
    }
    private static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using var stream = File.Create(path); encoder.Save(stream);
    }
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
}
