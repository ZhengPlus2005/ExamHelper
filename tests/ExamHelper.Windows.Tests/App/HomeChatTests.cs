using System.Windows.Input;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Images;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class HomeChatTests
{
    [Fact] public async Task HomeSendsWithoutAnyTestAndAuxiliaryAlwaysStartsNewConversation()
    {
        await using var rig = new ConversationWorkspaceRig(); var verification = new ServiceVerification(new byte[32]); var host = new Host();
        var home = new HomeViewModel(rig.Workspace,rig.Store,new StubServices(),() => Task.FromResult(rig.Settings),verification,host,() => Task.CompletedTask);
        await home.NewConversationAsync(default); await home.SetDraftTextAsync("home question",default);
        var answer = rig.Ai.QueueAttempt(); await home.SendAsync(default); await answer.Started.Task; answer.Complete(); await rig.Workspace.Selected!.WaitForIdleAsync(default);
        Assert.Single(home.Messages,m => m.Message.Role == MessageRole.User); Assert.False(verification.IsAsrVerified(rig.Settings));
        var main = rig.Workspace.Selected!; await main.UpdateDraftTextAsync("keep home draft",default);
        rig.Settings = rig.Settings with { Ai = rig.Settings.Ai with { SecretFingerprint = "a" },Asr = rig.Settings.Asr with { SecretFingerprint = "b" } };
        rig.Settings = verification.RecordAsrSuccess(verification.RecordAiSuccess(rig.Settings,verification.AiFingerprint(rig.Settings)),verification.AsrFingerprint(rig.Settings));
        await home.StartAsync(default);
        Assert.NotEqual(main.State.Id,rig.Workspace.AuxiliarySessionId); Assert.Equal("keep home draft",main.State.Draft.Text);
        Assert.Empty(rig.Workspace.Selected!.State.Messages); Assert.False(rig.Workspace.Selected.State.Draft.HasContent);
    }
    [Theory]
    [InlineData(Key.Enter,ModifierKeys.None,false,true)]
    [InlineData(Key.Enter,ModifierKeys.Shift,false,false)]
    [InlineData(Key.Enter,ModifierKeys.None,true,false)]
    [InlineData(Key.ImeProcessed,ModifierKeys.None,false,false)]
    public void EnterHonorsNewlineAndImeComposition(Key key,ModifierKeys mods,bool composing,bool send) => Assert.Equal(send,HomeInputBehavior.ShouldSend(key,mods,composing));
    [Fact] public async Task ImportTargetsOriginalConversationAndNeverAutomaticallySends()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(false,default);
        var importer = new Importer(); var draft = new HomeDraftController(() => rig.Workspace.Selected,importer);
        var import = draft.ImportFilesAsync(["local.png"],default); await importer.Entered.Task;
        var b = await rig.Workspace.NewAsync(false,default); await b.UpdateDraftTextAsync("B",default);
        importer.Release.TrySetResult(); await import;
        Assert.Single(a.State.Draft.Images); Assert.Empty(a.State.Messages); Assert.Empty(b.State.Draft.Images); Assert.Equal("B",b.State.Draft.Text);
    }
    [Fact] public async Task HomeMessagesKeepPicturesInsideTheirOwningMessage()
    {
        await using var rig = new ConversationWorkspaceRig(); var image = new ImageRef(Guid.NewGuid(),"image.png",20,10);
        var snapshot = new SessionSnapshot { Messages = [new() { Role = MessageRole.User,Text = "first",Images = [image] },new() { Role = MessageRole.Assistant,Text = "answer" },new() { Role = MessageRole.User,Text = "last" }] };
        await rig.Store.SaveAsync(snapshot,default);
        var home = new HomeViewModel(rig.Workspace,rig.Store,new StubServices(),() => Task.FromResult(rig.Settings),new(new byte[32]),new Host(),() => Task.CompletedTask);
        await home.SelectAsync(snapshot.Id,default);
        Assert.Equal(image,Assert.Single(home.Messages[0].Images)); Assert.Empty(home.Messages[1].Images); Assert.Empty(home.Messages[2].Images);
    }
    [Fact] public async Task EditingBeforeVoiceIsDisplayedDoesNotOverwriteTheNewTranscript()
    {
        await using var rig = new ConversationWorkspaceRig(); var a=await rig.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("base",default); var editor=new HomeDraftController(() => rig.Workspace.Selected,new Importer());
        editor.ObserveText(a.State.Id,"base");
        await a.AppendSpeechResultAsync(new(a.State.Id,a.State.SpeechGeneration,Guid.NewGuid(),"voice"),default);
        await editor.UpdateTextAsync("base edited",default);
        Assert.Equal("base edited"+Environment.NewLine+"voice",a.State.Draft.Text);
    }
    [Fact] public async Task HomeWindowPlacesRoundedPicturesBeforeTextAndViewerSupportsZoomAndEscape()
    {
        await StaTest.RunAsync(() =>
        {
            var root=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"ExamHelper-home-ui-"+Guid.NewGuid().ToString("N")); System.IO.Directory.CreateDirectory(root);
            var rig=new ConversationWorkspaceRig();
            try
            {
                var images=new ExamHelper.Windows.Capture.FileImageStore(root); var importer=new ImageImportService(images);
                var pixels=new byte[256*128*4]; for(var i=0;i<pixels.Length;i+=4) { pixels[i]=180; pixels[i+1]=120; pixels[i+2]=70; pixels[i+3]=255; }
                var bitmap=System.Windows.Media.Imaging.BitmapSource.Create(256,128,96,96,System.Windows.Media.PixelFormats.Bgra32,null,pixels,256*4); bitmap.Freeze();
                var image=Assert.Single(importer.ImportBitmapAsync(bitmap,default).GetAwaiter().GetResult());
                var snapshot=new SessionSnapshot { Selection=ModelCatalog.Default(rig.Settings),Messages=[new() { Role=MessageRole.User,Text="这张图片的题目",Images=[image],Status=MessageStatus.Complete },new() { Role=MessageRole.Assistant,Text="选择 A，因为满足条件。",Status=MessageStatus.Complete }] };
                rig.Store.SaveAsync(snapshot,default).GetAwaiter().GetResult(); rig.Workspace.OpenAsync(snapshot.Id,default).GetAwaiter().GetResult();
                var vm=new HomeViewModel(rig.Workspace,rig.Store,images,() => Task.FromResult(rig.Settings),new(new byte[32]),new Host(),() => Task.CompletedTask);
                var window=new HomeWindow(vm,() => throw new NotSupportedException(),() => Task.CompletedTask,importer);
                try
                {
                    window.Show(); window.UpdateLayout(); StaTest.Pump();
                    var messages=Assert.IsType<System.Windows.Controls.StackPanel>(window.FindName("HistoryMessages"));
                    var first=Assert.IsType<System.Windows.Controls.StackPanel>(messages.Children[0]);
                    var pictures=Assert.IsType<System.Windows.Controls.WrapPanel>(first.Children[1]);
                    Assert.IsType<System.Windows.Controls.TextBlock>(first.Children[2]);
                    var button=Assert.IsType<System.Windows.Controls.Button>(pictures.Children[0]); var grid=Assert.IsType<System.Windows.Controls.Grid>(button.Content);
                    var preview=Assert.IsType<System.Windows.Controls.Image>(grid.Children[0]); Assert.IsType<System.Windows.Media.RectangleGeometry>(preview.Clip);
                    var input=Assert.IsType<System.Windows.Controls.TextBox>(window.FindName("HomeDraftInput")); input.Text="主窗口正常输入"; StaTest.Pump();
                    window.DraftController.FlushAsync(default).GetAwaiter().GetResult(); Assert.Equal("主窗口正常输入",rig.Workspace.Selected!.State.Draft.Text);
                    var output=Environment.GetEnvironmentVariable("EXAMHELPER_TEST_UI_OUTPUT");
                    if(!string.IsNullOrEmpty(output))
                    {
                        System.IO.Directory.CreateDirectory(output); var render=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32); render.Render(window);
                        var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(render)); using var file=System.IO.File.Create(System.IO.Path.Combine(output,"home-chat.png")); png.Save(file);
                    }
                    var viewer=new ExamHelper.App.Images.ImageViewerWindow(bitmap) { Owner=window }; viewer.Show(); viewer.UpdateLayout(); StaTest.Pump(); var before=viewer.ZoomFactor;
                    viewer.ZoomAt(1.2,new System.Windows.Point(100,100)); Assert.True(viewer.ZoomFactor>before);
                    viewer.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,System.Windows.PresentationSource.FromVisual(viewer),0,Key.Escape) { RoutedEvent=Keyboard.PreviewKeyDownEvent }); Assert.False(viewer.IsVisible);
                }
                finally { window.Close(); StaTest.Pump(); }
            }
            finally { rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); System.IO.Directory.Delete(root,true); }
        });
    }
    [Fact] public async Task OrdinaryModelSelectorRestoresSessionThinkingAndManualSwitchLoadsDefaults()
    {
        await StaTest.RunAsync(() =>
        {
            var model=new AiModelProfile { Name="kimi-k3",DefaultThinking=new(ThinkingKind.Effort,"low") };
            var other=new AiModelProfile { Name="other" }; var profile=new AiServiceProfile { Name="A",Models=[model,other],DefaultModelId=model.Id };
            var settings=AiProfileCatalog.Normalize(new() { AiProfiles=[profile],ActiveAiProfileId=profile.Id });
            var selector=new ExamHelper.App.Controls.ModelSelector(); ModelSelection? requested=null; selector.SelectionRequested+=choice => requested=choice;
            selector.Configure(settings,ModelCatalog.Select(profile,model,new(ThinkingKind.Effort,"high")),false,new(new byte[32]));
            var thinking=Assert.IsType<System.Windows.Controls.ComboBox>(selector.FindName("Thinking")); Assert.Equal(new ThinkingChoice(ThinkingKind.Effort,"high"),thinking.SelectedItem);
            var models=Assert.IsType<System.Windows.Controls.ComboBox>(selector.FindName("Models")); models.SelectedItem=selector.ModelChoices.Single(m => m.Id==other.Id);
            Assert.Equal(other.Id,requested!.ModelId); Assert.Equal(ThinkingKind.ServiceDefault,requested.Thinking.Kind); Assert.Equal("low",model.DefaultThinking.Effort);
            requested=null;
            selector.Configure(settings,new(Guid.NewGuid(),Guid.NewGuid(),new()) { ProfileName="已删除配置",ModelName="原模型" },false,new(new byte[32]));
            Assert.Null(requested); Assert.Contains("已删除配置",Assert.IsType<System.Windows.Controls.TextBlock>(selector.FindName("Unavailable")).Text);
        });
    }
    private sealed class Host : IOverlayHost
    { public Task<ProtectionResult> OpenAsync(CancellationToken ct) => Task.FromResult(new ProtectionResult(true,0,0x11)); public void Show() { } public void Close() { } }
    private sealed class Importer : IImageImporter
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<ImageRef>> ImportFilesAsync(IEnumerable<string> files,CancellationToken ct) { Entered.TrySetResult(); await Release.Task.WaitAsync(ct); return [new(Guid.NewGuid(),"owned.png",20,10)]; }
        public Task<IReadOnlyList<ImageRef>> ImportBitmapAsync(System.Windows.Media.Imaging.BitmapSource bitmap,CancellationToken ct) => ImportFilesAsync([],ct);
    }
}
