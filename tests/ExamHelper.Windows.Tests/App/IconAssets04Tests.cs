using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class IconAssets04Tests
{
    [Fact] public async Task ApprovedSourceAndSevenRoundedIconSizesAreEmbeddedConsistently()
    {
        await StaTest.RunAsync(()=>
        {
            var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null && !File.Exists(Path.Combine(root.FullName,"ExamHelper.slnx")))root=root.Parent;Assert.NotNull(root);
            var assets=Path.Combine(root!.FullName,"src/ExamHelper.App/Assets");var source=Path.Combine(assets,"app-icon-source.jpg");Assert.True(File.Exists(source));
            Assert.Equal(SHA256.HashData(File.ReadAllBytes(Path.Combine(root.FullName,"docs/superpowers/specs/assets/examhelper-0.4/app-icon-reference.jpg"))),SHA256.HashData(File.ReadAllBytes(source)));
            var png=BitmapDecoder.Create(new Uri(Path.Combine(assets,"app-icon.png")),BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];Assert.Equal(256,png.PixelWidth);CheckPixels(png);
            var bytes=File.ReadAllBytes(Path.Combine(assets,"app-icon.ico"));using var reader=new BinaryReader(new MemoryStream(bytes));Assert.Equal(0,reader.ReadUInt16());Assert.Equal(1,reader.ReadUInt16());Assert.Equal(7,reader.ReadUInt16());
            var sizes=new List<int>();for(var i=0;i<7;i++) { var size=reader.ReadByte();sizes.Add(size==0?256:size);reader.ReadByte();reader.ReadByte();reader.ReadByte();reader.ReadUInt16();reader.ReadUInt16();var length=reader.ReadUInt32();var offset=reader.ReadUInt32();using var payload=new MemoryStream(bytes[(int)offset..(int)(offset+length)]);var decoded=BitmapDecoder.Create(payload,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];Assert.Equal(sizes[^1],decoded.PixelWidth);CheckPixels(decoded); }
            Assert.Equal(new[] { 16,24,32,48,64,128,256 },sizes);
            using var resource=Application.GetResourceStream(new Uri("pack://application:,,,/ExamHelper.App;component/Assets/app-icon.ico")).Stream;using var copy=new MemoryStream();resource.CopyTo(copy);Assert.Equal(bytes,copy.ToArray());
        });
    }
    [Fact] public async Task ActualTrayAndWindowReadTheSameIconAndProcessIdentityIsDistinct()
    {
        await StaTest.RunAsync(()=>
        {
            ExamHelper.Windows.Native.ApplicationIdentity.Apply();Assert.Equal(0,GetCurrentProcessExplicitAppUserModelID(out var pointer));try { Assert.Equal("ExamHelper.Desktop.0.4",System.Runtime.InteropServices.Marshal.PtrToStringUni(pointer)); }finally { System.Runtime.InteropServices.Marshal.FreeCoTaskMem(pointer); }
            using var tray=new ExamHelper.App.Lifecycle.TrayIconService();Assert.True(tray.IsVisible);
            var stream=(Stream)typeof(ExamHelper.App.Lifecycle.TrayIconService).GetField("_iconStream",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(tray)!;stream.Position=0;using var trayBytes=new MemoryStream();stream.CopyTo(trayBytes);
            using var resource=Application.GetResourceStream(new Uri("pack://application:,,,/ExamHelper.App;component/Assets/app-icon.ico")).Stream;using var expected=new MemoryStream();resource.CopyTo(expected);Assert.Equal(expected.ToArray(),trayBytes.ToArray());
            var usage=new ExamHelper.App.Help.UsageWindow(ExamHelper.Core.Settings.HotkeyBinding.Defaults);Assert.NotNull(usage.Icon);usage.Close();
        });
    }
    [System.Runtime.InteropServices.DllImport("shell32.dll")]
    private static extern int GetCurrentProcessExplicitAppUserModelID(out nint pointer);
    private static void CheckPixels(BitmapSource source)
    {
        var bitmap=new FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);Assert.Equal(0,pixel[3]);bitmap.CopyPixels(new Int32Rect(source.PixelWidth/2,2,1,1),pixel,4,0);Assert.Equal(255,pixel[3]);Assert.All(pixel.Take(3),color=>Assert.InRange(color,(byte)248,(byte)255));
    }
}
