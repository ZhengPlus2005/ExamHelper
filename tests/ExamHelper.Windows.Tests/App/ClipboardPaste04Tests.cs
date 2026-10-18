using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Home;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Images;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class ClipboardPaste04Tests
{
    [Fact]
    public async Task PngStreamAndBitmapAndPackedDibKeepOriginalPixels()
    {
        await StaTest.RunAsync(() =>
        {
            var bitmap=Bitmap();var png=Encode(bitmap,new PngBitmapEncoder());var bmp=Encode(bitmap,new BmpBitmapEncoder());
            var pngStream=new MemoryStream(png);pngStream.Position=pngStream.Length;
            var cases=new[] { new DataObject("PNG",pngStream),new DataObject(DataFormats.Bitmap,new WriteableBitmap(bitmap)),new DataObject(DataFormats.Dib,new MemoryStream(bmp[14..])) };
            foreach(var data in cases)
            {
                var payload=new ClipboardImageReader().Read(data);Assert.NotNull(payload?.Bitmap);Assert.Equal(64,payload!.Bitmap!.PixelWidth);Assert.Equal(32,payload.Bitmap.PixelHeight);Assert.True(payload.Bitmap.IsFrozen);
                var converted=new FormatConvertedBitmap(payload.Bitmap,PixelFormats.Bgra32,null,0);var pixel=new byte[4];converted.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);Assert.Equal(new byte[] { 13,27,203,255 },pixel);
            }
            Assert.Equal(pngStream.Length,pngStream.Position);Assert.True(pngStream.CanRead);
            Assert.Null(new ClipboardImageReader().Read(new DataObject(DataFormats.UnicodeText,"普通文字")));
            var files=new DataObject(DataFormats.FileDrop,new[] { "C:/owned-test/a.png","C:/owned-test/b.png" });
            Assert.Equal(new[] { "C:/owned-test/a.png","C:/owned-test/b.png" },new ClipboardImageReader().Read(files)!.Files);
        });
    }
    [Fact]
    public async Task PasteCommandAcceptsImageOnlyWithoutReadingClipboardDuringStatusPolling()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig();var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();runtime.UpdateDraftTextAsync("base",default).GetAwaiter().GetResult();
            var data=new DataObject();data.SetData("PNG",new MemoryStream(Encode(Bitmap(),new PngBitmapEncoder())));data.SetData(DataFormats.Html,"<img src='https://example.invalid/image'>");data.SetText("https://example.invalid/image");
            var clipboard=new Clipboard(data);var input=new TextBox { Text="base",AcceptsReturn=true };var errors=new List<Exception>();
            var drafts=new HomeDraftController(()=>rig.Workspace.Selected,new ImageImportService(new StubServices()));
            using var behavior=new HomePasteBehavior(input,drafts,clipboard,errors.Add);
            try
            {
                for(var i=0;i<20;i++)Assert.True(ApplicationCommands.Paste.CanExecute(null,input));Assert.Equal(0,clipboard.Reads);
                ApplicationCommands.Paste.Execute(null,input);drafts.DrainAllAsync(default).GetAwaiter().GetResult();StaTest.Pump();
                Assert.Equal(1,clipboard.Reads);Assert.Empty(errors);Assert.Equal("base",input.Text);Assert.Equal("base",runtime.State.Draft.Text);Assert.Empty(runtime.State.Messages);
                var image=Assert.Single(runtime.State.Draft.Images);Assert.Equal(64,image.Width);Assert.Equal(32,image.Height);
                input.IsReadOnly=true;Assert.False(ApplicationCommands.Paste.CanExecute(null,input));
            }
            finally { rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact]
    public async Task InvalidImagePreservesDraftAndOrdinaryTextIsNotSwallowed()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig();var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();runtime.UpdateDraftTextAsync("keep",default).GetAwaiter().GetResult();
            var input=new TextBox { Text="keep" };var errors=new List<Exception>();var drafts=new HomeDraftController(()=>rig.Workspace.Selected,new ImageImportService(new StubServices()));
            try
            {
                using(var behavior=new HomePasteBehavior(input,drafts,new Clipboard(new DataObject("PNG",new MemoryStream([1,2,3]))),errors.Add))
                { ApplicationCommands.Paste.Execute(null,input);Assert.Single(errors);Assert.Equal("keep",runtime.State.Draft.Text);Assert.Empty(runtime.State.Draft.Images); }
                using(var behavior=new HomePasteBehavior(input,drafts,new Clipboard(new DataObject(DataFormats.UnicodeText,"ordinary")),errors.Add))
                {
                    var args=(ExecutedRoutedEventArgs)Activator.CreateInstance(typeof(ExecutedRoutedEventArgs),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[ApplicationCommands.Paste,null],null)!;args.RoutedEvent=CommandManager.PreviewExecutedEvent;
                    input.RaiseEvent(args);Assert.False(args.Handled);Assert.Equal("keep",input.Text);
                }
            }
            finally { rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    internal static BitmapSource Bitmap()
    {
        var pixels=new byte[64*32*4];for(var i=0;i<pixels.Length;i+=4) { pixels[i]=13;pixels[i+1]=27;pixels[i+2]=203;pixels[i+3]=255; }
        var bitmap=BitmapSource.Create(64,32,120,120,PixelFormats.Bgra32,null,pixels,64*4);bitmap.Freeze();return bitmap;
    }
    internal static byte[] Encode(BitmapSource bitmap,BitmapEncoder encoder)
    { encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray(); }
    private sealed class Clipboard(IDataObject data):IClipboardAccess
    { public int Reads;public IDataObject? GetDataObject() { Reads++;return data; } }
}
