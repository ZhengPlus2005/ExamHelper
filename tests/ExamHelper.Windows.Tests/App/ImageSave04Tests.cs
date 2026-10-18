using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using ExamHelper.App.Images;
using ExamHelper.Windows.Images;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class ImageSave04Tests
{
    [Fact] public async Task OriginalPngSurvivesZoomPanAndCallerBufferChanges()
    {
        await StaTest.RunAsync(()=>
        {
            var root=Path.Combine(Path.GetTempPath(),"ExamHelper-save04-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
            var bitmap=ImageViewer04Tests.FourCorners(192);var bytes=ClipboardPaste04Tests.Encode(bitmap,new PngBitmapEncoder());var original=bytes.ToArray();
            var viewer=new ImageViewerWindow(bitmap,bytes);viewer.Show();viewer.UpdateLayout();StaTest.Pump();
            try { viewer.ZoomAt(2,new Point(100,100));viewer.PanBy(new Vector(-100,60));Array.Clear(bytes);var path=Path.Combine(root,"saved.png");viewer.SaveOriginalAsync(path,default).GetAwaiter().GetResult();Assert.Equal(original,File.ReadAllBytes(path)); }
            finally { viewer.Close();Directory.Delete(root,true); }
        });
    }
    [Fact] public async Task CancelledOrReadOnlyTargetKeepsOriginalAndCleansTemporaryFiles()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-save04-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"saved.png");await File.WriteAllBytesAsync(path,[7,8,9]);
        try
        {
            using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new ImageSaveService().SaveAsync(path,new byte[] { 1,2,3 },cancel.Token));Assert.Equal(new byte[] { 7,8,9 },await File.ReadAllBytesAsync(path));
            File.SetAttributes(path,FileAttributes.ReadOnly);await Assert.ThrowsAnyAsync<Exception>(()=>new ImageSaveService().SaveAsync(path,new byte[] { 1,2,3 },default));Assert.Equal(new byte[] { 7,8,9 },await File.ReadAllBytesAsync(path));Assert.Single(Directory.GetFiles(root));
        }
        finally { File.SetAttributes(path,FileAttributes.Normal);Directory.Delete(root,true); }
    }
    [Fact] public async Task PixelTransformFitsAndKeepsZoomAnchorFixed()
    {
        await StaTest.RunAsync(()=>
        {
            var transform=new ImageViewportTransform();transform.Fit(new Size(2560,1440),new Size(800,600));
            Assert.Equal(new Point(0,75),transform.PixelToViewport.Transform(new Point(0,0)));Assert.Equal(new Point(800,525),transform.PixelToViewport.Transform(new Point(2560,1440)));
            var pixel=transform.PixelToViewport.Inverse!.Transform(new Point(200,200));transform.ZoomAt(2,new Point(200,200));Assert.Equal(new Point(200,200),transform.PixelToViewport.Transform(pixel));
            transform.Pan(new Vector(10,-20));Assert.Equal(new Point(210,180),transform.PixelToViewport.Transform(pixel));
        });
    }
}
