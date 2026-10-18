using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Images;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class ImageImportTests
{
    [Fact] public async Task ImportsCommonFormatsAtFullResolutionAndRejectsUnknownInput()
    {
        await StaTest.RunAsync(() =>
        {
            var root = Path.Combine(Path.GetTempPath(),"ExamHelper-import-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var images = new FileImageStore(Path.Combine(root,"images")); var importer = new ImageImportService(images);
                var pixels = new byte[64*32*4]; for(var i=3;i<pixels.Length;i+=4) pixels[i]=255;
                var bitmap = BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,pixels,64*4); bitmap.Freeze();
                BitmapEncoder[] encoders = [new PngBitmapEncoder(),new JpegBitmapEncoder(),new BmpBitmapEncoder(),new GifBitmapEncoder(),new TiffBitmapEncoder()];
                for(var i=0;i<encoders.Length;i++)
                {
                    encoders[i].Frames.Add(BitmapFrame.Create(bitmap));
                    if(encoders[i] is GifBitmapEncoder or TiffBitmapEncoder) { var white=BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,Enumerable.Repeat((byte)255,pixels.Length).ToArray(),64*4); encoders[i].Frames.Add(BitmapFrame.Create(white)); }
                    var path = Path.Combine(root,"source"+i+".img"); using(var file=File.Create(path)) encoders[i].Save(file);
                    var imported = importer.ImportFilesAsync([path],default).GetAwaiter().GetResult(); var image = Assert.Single(imported); Assert.Equal(64,image.Width); Assert.Equal(32,image.Height);
                    var bytes = images.ReadAsync(image,default).GetAwaiter().GetResult(); Assert.Equal(new byte[] { 137,80,78,71 },bytes[..4]);
                    using var decodedSource=new MemoryStream(bytes); var decoded=BitmapDecoder.Create(decodedSource,BitmapCreateOptions.None,BitmapCacheOption.OnLoad).Frames[0];
                    var bgra=new FormatConvertedBitmap(decoded,PixelFormats.Bgra32,null,0); var firstPixel=new byte[4]; bgra.CopyPixels(new System.Windows.Int32Rect(0,0,1,1),firstPixel,4,0); Assert.Equal(0,firstPixel[0]);
                }
                var bad = Path.Combine(root,"bad.txt"); File.WriteAllText(bad,"not an image");
                Assert.ThrowsAny<Exception>(() => importer.ImportFilesAsync([bad],default).GetAwaiter().GetResult());
            }
            finally { Directory.Delete(root,true); }
        });
    }
}
