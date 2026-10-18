using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Controls;
using ExamHelper.App.Images;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.InteractionLab;
public static class DisplayVerification
{
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);var monitors=new List<PhysicalWindowPlacement>();
        MonitorCallback callback=(nint handle,nint dc,ref NativeRect bounds,nint data)=> { monitors.Add(new(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top));return true; };
        if(!EnumDisplayMonitors(0,0,callback,0))throw new InvalidOperationException("Cannot enumerate monitors.");
        var results=new List<object>();var dpis=new List<double>();var all=true;
        for(var index=0;index<monitors.Count;index++)
        {
            var monitor=monitors[index];var work=MonitorGeometry.ForPlacement(monitor).WorkArea;
            await using var fixture=await ProductCaptureFixture.CreateAsync(new(work.X+32,work.Y+32,Math.Min(800,work.Width-64),Math.Min(720,work.Height-64)));
            fixture.Window.Show();await Task.Delay(150);fixture.Window.UpdateLayout();var dpi=VisualTreeHelper.GetDpi(fixture.Window).PixelsPerInchX;dpis.Add(dpi);
            var checks=new Dictionary<string,bool> { ["ProtectionReadback17"]=fixture.Window.Protection.Success && fixture.Window.Protection.EffectiveAffinity==17 };
            foreach(var first in new[] { "NewButton","DirectButton" })
            {
                var button=(Button)fixture.Window.FindName(first);var row=(Panel)button.Parent;checks[first+"RowEqual"]=row.Children.OfType<Button>().All(item=>Math.Abs(item.ActualWidth-button.ActualWidth)<0.1);
                checks[first+"Outline"]=button.Template.FindName("Outline",button) is DwellOutline;
            }
            var encodedChecks=new List<object>();
            foreach(var encodedDpi in new[] { 96,120,192 })
            {
                var source=HomeInteractionVerification.OwnedBitmap(encodedDpi);var png=HomeInteractionVerification.Encode(source);using var stream=new MemoryStream(png);var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
                var viewer=new ImageViewerWindow(bitmap,png);new WindowInteropHelper(viewer).EnsureHandle();MonitorGeometry.SetBounds(new WindowInteropHelper(viewer).Handle,new(work.X+48,work.Y+48,Math.Min(1000,work.Width-96),Math.Min(720,work.Height-96)));viewer.Show();await Task.Delay(80);viewer.UpdateLayout();viewer.Fit();viewer.UpdateLayout();
                try
                {
                    var picture=(Image)viewer.FindName("Picture");var top=picture.TransformToAncestor(viewer).Transform(new Point());var bottom=picture.TransformToAncestor(viewer).Transform(new Point(2560,1440));
                    var render=new RenderTargetBitmap((int)Math.Ceiling(viewer.ActualWidth),(int)Math.Ceiling(viewer.ActualHeight),96,96,PixelFormats.Pbgra32);render.Render(viewer);
                    var corners=Pixel(render,top.X+12,top.Y+12,Colors.Red) && Pixel(render,bottom.X-12,top.Y+12,Colors.Lime) && Pixel(render,top.X+12,bottom.Y-12,Colors.Blue) && Pixel(render,bottom.X-12,bottom.Y-12,Colors.Yellow);
                    viewer.ZoomAt(2,new Point(100,100));viewer.PanBy(new Vector(-60,30));var saved=Path.Combine(directory,$"monitor-{index}-image-{encodedDpi}.png");await viewer.SaveOriginalAsync(saved,default);var bytes=await File.ReadAllBytesAsync(saved);var exact=png.SequenceEqual(bytes);
                    checks[$"Image{encodedDpi}FullCorners"]=corners;checks[$"Image{encodedDpi}OriginalSave"]=exact;encodedChecks.Add(new { EncodedDpi=encodedDpi,bitmap.PixelWidth,bitmap.PixelHeight,FullCorners=corners,ExactOriginal=exact });
                }
                finally { viewer.Close(); }
            }
            fixture.Window.UpdateLayout();HomeInteractionVerification.SaveVisual(fixture.Window,Path.Combine(directory,$"monitor-{index}-overlay.png"));all&=checks.Values.All(value=>value);results.Add(new { Monitor=monitor,Dpi=dpi,Checks=checks,Images=encodedChecks });
        }
        var required=monitors.Count>=2 && monitors.Any(monitor=>monitor.X<0) && dpis.Contains(120) && dpis.Contains(192);
        var report=new { Passed=all && monitors.Count>0,Monitors=results,NetworkRequests=0,UserSettingsRead=false,RequiredNegative125200Available=required,Unavailable=required?Array.Empty<string>():new[] { "No attached physical negative-coordinate dual-screen combination with both 125% and 200% DPI; no display settings were changed." },Events="Actual owned product HWNDs moved to every attached monitor; own WPF pixel renders only. Physical pointer interactions are recorded separately." };
        await File.WriteAllTextAsync(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));if(!report.Passed)throw new InvalidOperationException("Display verification failed.");
    }
    private static bool Pixel(BitmapSource bitmap,double x,double y,Color expected)
    { if(x<0 || y<0 || x>=bitmap.PixelWidth || y>=bitmap.PixelHeight)return false;var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect((int)x,(int)y,1,1),pixel,4,0);return pixel.SequenceEqual(new byte[] { expected.B,expected.G,expected.R,255 }); }
    [StructLayout(LayoutKind.Sequential)]private struct NativeRect { public int Left,Top,Right,Bottom; }
    private delegate bool MonitorCallback(nint handle,nint dc,ref NativeRect bounds,nint data);
    [DllImport("user32.dll")]private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
}
