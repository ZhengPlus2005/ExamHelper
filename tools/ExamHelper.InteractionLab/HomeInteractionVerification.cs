using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Composition;
using ExamHelper.App.Controls;
using ExamHelper.App.Home;
using ExamHelper.App.Images;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Markdown;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Images;
using ExamHelper.Windows.Persistence;
using ExamHelper.Windows.Settings;
namespace ExamHelper.InteractionLab;
public static class HomeInteractionVerification
{
    public static Task RunAsync(string directory)=>RunAsync(directory,false);
    public static async Task RunAsync(string directory,bool keepOpen)
    {
        Directory.CreateDirectory(directory);var checks=new Dictionary<string,bool>();
        var layout=new AppDataLayout(Path.Combine(directory,"owned-data"));
        await using(var startup=await AppComposition.CreateAsync(paths:new(Path.Combine(directory,"owned-blank-startup"))))
        {
            checks["RealBlankStartup"]=startup.CurrentSettings.AiProfiles!.Value.Length==0 && startup.CurrentSettings.ActiveAiProfileId is null;
            var half=new AppSettings { AiProfiles=[new() { Name="半填配置",BaseAddress="https://" }] };
            await startup.SettingsStore.SaveAsync(half,default);var loaded=await startup.SettingsStore.LoadAsync(default);
            checks["PartialConfigurationRoundTrip"]=loaded.AiProfiles!.Value.Single().BaseAddress=="https://" && !ProfileCompleteness.Evaluate(loaded.AiProfiles.Value.Single()).Complete;
        }
        var proof=new ServiceVerification(new byte[32]);var settings=TestSettings.Configured();var clock=new ManualTimeProvider();var ai=new DeferredAiClient();var stub=new StubServices();
        var bitmap=OwnedBitmap(192);var png=Encode(bitmap);var references=new ImageReferenceRegistry();var images=new FileImageStore(layout.Images,references);var store=new FailingStore(new SqliteSessionStore(layout.Database,references));
        using var handler=new FakeHttpHandler((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content=new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"EH7Q9\"},\"finish_reason\":null}]}\n\ndata: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") }));
        using(var http=new HttpClient(handler))
        {
            var secrets=new InMemorySecretStore();await secrets.SetAsync(settings.Ai.SecretId,"own-test-key",default);
            var runner=new ServiceTestRunner(http,secrets,proof,png);var profile=settings.AiProfiles!.Value[0];var model=profile.Models!.Value[0];
            foreach(var kind in new[] { AiTestKind.Text,AiTestKind.Image })
            {
                var result=kind==AiTestKind.Text?await runner.TestTextAsync(settings,default):await runner.TestImageAsync(settings,default);
                Check(result.Passed,"fake service test");settings=proof.RecordModelResult(settings,profile.Id,model.Id,kind,result.Passed,result.Fingerprint);
            }
            checks["RealTextAndImageTestRunner"]=handler.Calls==2 && proof.CanUseInAuxiliary(settings,ModelCatalog.Default(settings));
        }
        settings=settings with { Hotkeys=new() { Bindings=[] } };
        var workspace=new ConversationWorkspace(store,()=>new SessionCoordinator(ai,stub,stub,new OwnedCapture(png),images,()=>settings,store,clock,verification:proof));
        var initial=await workspace.NewAsync(false,default);HomeViewModel? home=null;
        var host=new OverlayHost(initial,images,()=>settings,_=>Task.CompletedTask,()=>home!.ReturnedAsync(default),()=>Task.CompletedTask,workspace:workspace,verification:proof);
        home=new HomeViewModel(workspace,store,images,()=>Task.FromResult(settings),proof,host,()=>Task.CompletedTask);
        var window=new HomeWindow(home,()=>throw new NotSupportedException("Fixture has no real settings access."),()=>Task.CompletedTask,new ImageImportService(images)) { Title="ExamHelper 0.4 · 自有测试数据" };
        var retained=false;
        try
        {
            window.Show();await Task.Delay(80);await home.StartAsync(default);await Task.Delay(80);
            var runtime=workspace.Selected!;Check(host.CurrentWindow is { Protection.Success:true },"protected entry");
            checks["ProtectedEntryWithoutAsr"]=host.CurrentWindow!.Protection.EffectiveAffinity==17 && !runtime.CanExecute(AppCommand.ToggleRecording);
            await runtime.ExecuteAsync(AppCommand.CaptureToDraft,default);await runtime.AppendSpeechResultAsync(new(runtime.State.Id,runtime.State.SpeechGeneration,Guid.NewGuid(),"自有四角图片问题"),default);
            var image=runtime.State.Draft.Images.Single();await home.ReturnedAsync(default);await Task.Delay(100);
            checks["AuxiliaryDraftReturnsToHome"]=runtime.State.Draft.Images.Length==1 && runtime.State.Draft.Text.Contains("自有四角图片");
            var tile=Descendants(window).OfType<DraftImageTile>().Single();tile.PreviewCommand!.Execute(null);await Task.Delay(100);
            var viewer=window.OwnedWindows.OfType<ImageViewerWindow>().Single();viewer.ZoomAt(1.5,new Point(100,100));viewer.PanBy(new Vector(-80,60));await viewer.SaveOriginalAsync(Path.Combine(directory,"saved-original.png"),default);viewer.Close();
            var saved=await File.ReadAllBytesAsync(Path.Combine(directory,"saved-original.png"));checks["PreviewSaveFullOriginal"]=png.SequenceEqual(saved);
            var attempt=ai.QueueAttempt();await window.DraftController.SendAsync(default);await attempt.Started.Task;clock.Advance(TimeSpan.FromSeconds(12.4));
            attempt.Text("# 自有示例\n\n**答案**\n\n```cpp\nint answer = 42;\n"+new string(' ',30)+"// "+new string('x',180)+"\n```\n\n"+string.Join("\n\n",Enumerable.Range(0,35).Select(i=>$"用于滚动检查的段落 {i}。")));
            await Task.Delay(100);var frozen=runtime.SnapshotForPresentation();clock.Advance(TimeSpan.FromSeconds(1));attempt.Complete();await runtime.WaitForIdleAsync(default);await Task.Delay(100);
            checks["FinalTiming"]=runtime.State.Messages.Last().Timing is { IsRunning:false } timing && Math.Abs(timing.Elapsed.TotalSeconds-13.4)<0.001;
            var message=Descendants(window).OfType<HomeMessageView>().Last();checks["CopyOriginalMarkdown"]=message.CopyText==runtime.State.Messages.Last().Text;
            var export=new ConversationExportController(new(images,new MarkdownHtmlRenderer()));await export.WriteAsync(Path.Combine(directory,"conversation.html"),frozen,default);
            var html=await File.ReadAllTextAsync(Path.Combine(directory,"conversation.html"));checks["FrozenOfflineExport"]=html.Contains("已用 12 秒") && html.Contains("data:image/png;base64,") && !html.Contains("<script");
            var a=runtime.State.Id;var b=await workspace.NewAsync(false,default);await b.UpdateDraftTextAsync("待删 B",default);var c=await workspace.NewAsync(false,default);await c.UpdateDraftTextAsync("待删 C",default);await home.SelectAsync(a,default);await home.RefreshAsync(default);
            home.HistorySelection.Enter();home.HistorySelection.SetChecked(b.State.Id,true);home.HistorySelection.SetChecked(c.State.Id,true);store.FailDelete=b.State.Id;
            var result=await home.DeleteManyAsync([b.State.Id,c.State.Id],_=>Task.FromResult(true),default);checks["BatchPartialFailure"]=result.DeletedIds.SequenceEqual([c.State.Id]) && result.Failures.ContainsKey(b.State.Id) && home.HistorySelection.SelectedIds.Contains(b.State.Id);
            store.FailDelete=null;var retry=await home.DeleteManyAsync([b.State.Id],_=>Task.FromResult(true),default);checks["BatchRetry"]=retry.DeletedIds.SequenceEqual([b.State.Id]);home.HistorySelection.Complete();await home.RefreshAsync(default);await Task.Delay(100);
            window.UpdateLayout();SaveVisual(window,Path.Combine(directory,"home.png"));
            var passed=checks.Values.All(v=>v);await File.WriteAllTextAsync(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new { Passed=passed,Checks=checks,NetworkRequests=0,FakeHttpRequests=handler.Calls,UserSettingsRead=false,DataRoot=layout.Root,ImageWidth=bitmap.PixelWidth,ImageHeight=bitmap.PixelHeight,Dpi=VisualTreeHelper.GetDpi(window).PixelsPerInchX,Events="Real product HWND/WPF commands with synthetic data; physical input is verified separately." },new JsonSerializerOptions { WriteIndented=true }));Check(passed,"comprehensive workflow");
            if(keepOpen)
            {
                var source=new Window { Title="ExamHelper Owned Image",Width=560,Height=400,Content=new Image { Source=bitmap,Stretch=Stretch.Uniform } };
                var command=new System.Windows.Input.CommandBinding(System.Windows.Input.ApplicationCommands.Copy,(_,e)=> { Clipboard.SetImage(bitmap);e.Handled=true; },(_,e)=> { e.CanExecute=true;e.Handled=true; });source.CommandBindings.Add(command);source.InputBindings.Add(new System.Windows.Input.KeyBinding(System.Windows.Input.ApplicationCommands.Copy,System.Windows.Input.Key.C,System.Windows.Input.ModifierKeys.Control));source.Show();
                window.Closed+=async(_,_)=> { source.Close();host.Close();await workspace.DisposeAsync();Application.Current.Shutdown(); };retained=true;
            }
        }
        finally { if(!retained) { foreach(var child in window.OwnedWindows.OfType<Window>().ToArray())child.Close();window.Close();host.Close();await workspace.DisposeAsync(); } }
    }
    internal static BitmapSource OwnedBitmap(double dpi)
    {
        var pixels=new byte[2560*1440*4];for(var y=0;y<1440;y++)for(var x=0;x<2560;x++)
        { var color=y<720?(x<1280?Colors.Red:Colors.Lime):(x<1280?Colors.Blue:Colors.Yellow);var i=(y*2560+x)*4;pixels[i]=color.B;pixels[i+1]=color.G;pixels[i+2]=color.R;pixels[i+3]=255; }
        var bitmap=BitmapSource.Create(2560,1440,dpi,dpi,PixelFormats.Bgra32,null,pixels,2560*4);bitmap.Freeze();return bitmap;
    }
    internal static byte[] Encode(BitmapSource bitmap) { var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray(); }
    internal static void SaveVisual(FrameworkElement visual,string path) { var bitmap=new RenderTargetBitmap((int)Math.Ceiling(visual.ActualWidth),(int)Math.Ceiling(visual.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);File.WriteAllBytes(path,Encode(bitmap)); }
    internal static IEnumerable<DependencyObject> Descendants(DependencyObject root) { yield return root;for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var node in Descendants(VisualTreeHelper.GetChild(root,i)))yield return node; }
    private static void Check(bool condition,string name) { if(!condition)throw new InvalidOperationException("Failed: "+name); }
    private sealed class OwnedCapture(byte[] png):IScreenCapture { public Task<CapturedImage> CaptureAsync(CancellationToken ct)=>Task.FromResult(new CapturedImage(png.ToArray(),2560,1440)); }
    private sealed class FailingStore(ISessionStore inner):ISessionStore
    {
        public Guid? FailDelete;
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct)=>inner.SaveAsync(s,ct);
        public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)=>inner.LoadAsync(id,ct);
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)=>inner.ListAsync(ct);
        public Task DeleteAsync(Guid id,CancellationToken ct)=>FailDelete==id?Task.FromException(new ServiceFailure(FailureCategory.Storage,"Owned deletion failure")):inner.DeleteAsync(id,ct);
    }
}
