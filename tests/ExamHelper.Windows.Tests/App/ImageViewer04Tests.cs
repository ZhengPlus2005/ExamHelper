using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Images;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class ImageViewer04Tests
{
    [Theory]
    [InlineData(96)]
    [InlineData(120)]
    [InlineData(192)]
    public async Task RealViewportRendersAllFourCornersAtEveryImageDpi(double dpi)
    {
        await StaTest.RunAsync(()=>
        {
            var png=ClipboardPaste04Tests.Encode(FourCorners(dpi),new PngBitmapEncoder());using var stream=new MemoryStream(png);var bitmap=BitmapDecoder.Create(stream,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];var viewer=new ImageViewerWindow(bitmap,png) { Width=820,Height=660 };viewer.Show();viewer.UpdateLayout();StaTest.Pump();viewer.Fit();viewer.UpdateLayout();
            try
            {
                var viewport=Assert.IsAssignableFrom<FrameworkElement>(viewer.FindName("Viewport"));var picture=Assert.IsType<Image>(viewer.FindName("Picture"));
                var top=picture.TransformToAncestor(viewport).Transform(new Point(0,0));var bottom=picture.TransformToAncestor(viewport).Transform(new Point(2560,1440));
                Assert.True(top.X>=-0.01 && top.Y>=-0.01 && bottom.X<=viewport.ActualWidth+0.01 && bottom.Y<=viewport.ActualHeight+0.01);
                var render=new RenderTargetBitmap((int)Math.Ceiling(viewer.ActualWidth),(int)Math.Ceiling(viewer.ActualHeight),96,96,PixelFormats.Pbgra32);render.Render(viewer);
                var renderTop=picture.TransformToAncestor(viewer).Transform(new Point(0,0));var renderBottom=picture.TransformToAncestor(viewer).Transform(new Point(2560,1440));
                AssertPixel(render,renderTop.X+12,renderTop.Y+12,Colors.Red);AssertPixel(render,renderBottom.X-12,renderTop.Y+12,Colors.Lime);
                AssertPixel(render,renderTop.X+12,renderBottom.Y-12,Colors.Blue);AssertPixel(render,renderBottom.X-12,renderBottom.Y-12,Colors.Yellow);
                viewer.ZoomAt(1.2,new Point(200,200));Assert.True(viewer.ZoomFactor>0);
            }
            finally { viewer.Close();StaTest.Pump(); }
        });
    }
    internal static BitmapSource FourCorners(double dpi)
    {
        var pixels=new byte[2560*1440*4];
        for(var y=0;y<1440;y++)for(var x=0;x<2560;x++)
        { var color=y<720?(x<1280?Colors.Red:Colors.Lime):(x<1280?Colors.Blue:Colors.Yellow);var i=(y*2560+x)*4;pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=255; }
        var bitmap=BitmapSource.Create(2560,1440,dpi,dpi,PixelFormats.Bgra32,null,pixels,2560*4);bitmap.Freeze();return bitmap;
    }
    private static void AssertPixel(BitmapSource bitmap,double x,double y,Color expected)
    { var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect((int)x,(int)y,1,1),pixel,4,0);Assert.Equal(new byte[] { expected.B,expected.G,expected.R,255 },pixel); }
}
