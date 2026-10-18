using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Home;
using ExamHelper.App.Images;
using ExamHelper.App.Overlay;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Images;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class DraftImageTile04Tests
{
    [Fact]
    public async Task ClickingDraftBodyPreviewsOriginalAndOnlyCornerButtonDeletes()
    {
        await StaTest.RunAsync(() =>
        {
            var root=Path.Combine(Path.GetTempPath(),"ExamHelper-tile04-"+Guid.NewGuid().ToString("N"));var rig=new ConversationWorkspaceRig();HomeWindow? window=null;
            try
            {
                var images=new FileImageStore(root);var importer=new ImageImportService(images);var pixels=new byte[64*32*4];for(var i=3;i<pixels.Length;i+=4)pixels[i]=255;
                var bitmap=BitmapSource.Create(64,32,96,96,PixelFormats.Bgra32,null,pixels,64*4);bitmap.Freeze();
                var image=Assert.Single(importer.ImportBitmapAsync(bitmap,default).GetAwaiter().GetResult());
                var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();runtime.AppendImagesAsync([image],default).GetAwaiter().GetResult();
                var data=new DataObject("PNG",new MemoryStream(ClipboardPaste04Tests.Encode(bitmap,new PngBitmapEncoder())));
                var home=new HomeViewModel(rig.Workspace,rig.Store,images,()=>Task.FromResult(rig.Settings),rig.Verification,new Host(),()=>Task.CompletedTask);
                window=new HomeWindow(home,()=>throw new NotSupportedException(),()=>Task.CompletedTask,importer,new Clipboard(data));window.Show();window.UpdateLayout();StaTest.Pump();
                var input=Assert.IsType<TextBox>(window.FindName("HomeDraftInput"));System.Windows.Input.ApplicationCommands.Paste.Execute(null,input);window.DraftController.DrainAllAsync(default).GetAwaiter().GetResult();
                var render=typeof(HomeWindow).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[typeof(bool)],null)!;render.Invoke(window,[true]);window.UpdateLayout();StaTest.Pump();
                Assert.Equal(2,runtime.State.Draft.Images.Length);
                var pictures=Assert.IsType<WrapPanel>(window.FindName("HomeDraftPictures"));var body=Nodes(pictures).OfType<Button>().First();Invoke(body);StaTest.Pump();
                Assert.Equal(2,runtime.State.Draft.Images.Length);
                var wait=System.Diagnostics.Stopwatch.StartNew();while(!window.OwnedWindows.OfType<ImageViewerWindow>().Any() && wait.Elapsed<TimeSpan.FromSeconds(2)) { StaTest.Pump();Thread.Sleep(3); }
                var viewer=Assert.Single(window.OwnedWindows.OfType<ImageViewerWindow>());viewer.Close();StaTest.Pump();
                var remove=Nodes(pictures).OfType<Button>().First(b=>b.Name=="RemoveButton");Invoke(remove);StaTest.Pump();
                Assert.NotEqual(image.Id,Assert.Single(runtime.State.Draft.Images).Id);Assert.Empty(window.OwnedWindows.OfType<ImageViewerWindow>());
            }
            finally { if(window is not null) { foreach(var child in window.OwnedWindows.OfType<Window>().ToArray()) child.Close();window.Close(); }rig.DisposeAsync().AsTask().GetAwaiter().GetResult();if(Directory.Exists(root))Directory.Delete(root,true); }
        });
    }
    private static void Invoke(Button button)=>((System.Windows.Automation.Provider.IInvokeProvider)new System.Windows.Automation.Peers.ButtonAutomationPeer(button).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke)!).Invoke();
    private static IEnumerable<DependencyObject> Nodes(DependencyObject parent)
    { for(var i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++) { var child=VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Nodes(child))yield return nested; } }
    private sealed class Clipboard(IDataObject data):IClipboardAccess { public IDataObject? GetDataObject()=>data; }
    private sealed class Host:IOverlayHost
    { public Task<ProtectionResult> OpenAsync(CancellationToken ct)=>Task.FromResult(new ProtectionResult(true,0,17));public void Show() { } public void Close() { } }
}
