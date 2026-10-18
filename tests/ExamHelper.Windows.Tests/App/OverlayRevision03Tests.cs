using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using ExamHelper.App.Controls;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class OverlayRevision03Tests
{
    [Fact] public async Task ActionsStayBelowDraftAndFoldLabelReflectsItsNextAction()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig=new Rig(); var window=rig.Host.CurrentWindow!; window.Show(); window.UpdateLayout(); StaTest.Pump();
            Assert.DoesNotContain(Nodes(window).OfType<TextBlock>(),t => t.Text=="悬停操作 · 主页／隐藏两秒");
            var fold=Assert.IsType<Button>(window.FindName("FoldButton")); Assert.Equal("折叠",fold.Content); window.ToggleCollapse(); Assert.Equal("展开",fold.Content); window.ToggleCollapse(); Assert.Equal("折叠",fold.Content);
            var buttons=Assert.IsType<WrapPanel>(window.FindName("ActionButtons"));
            Assert.Equal(new[] { "DirectButton","CaptureButton","RecordButton","ClearButton","SendButton" },buttons.Children.OfType<Button>().Take(5).Select(b => b.Name));
            window.Width=480; window.Height=360; window.UpdateLayout(); StaTest.Pump();
            var send=Assert.IsType<Button>(window.FindName("SendButton")); var bottom=send.TranslatePoint(new Point(0,send.ActualHeight),window).Y; Assert.InRange(bottom,1,window.ActualHeight-5);
        });
    }
    [Fact] public async Task ThumbnailHasGrayDeletionOverlayAndRedCircularProgressWithoutCaption()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig=new Rig(); var window=rig.Host.CurrentWindow!;
            rig.Coordinator.AppendImagesAsync([new(Guid.NewGuid(),"test.png",1920,1080)],default).GetAwaiter().GetResult(); window.Show(); window.UpdateLayout(); StaTest.Pump();
            var view=Assert.IsType<OverlayViewModel>(window.DataContext); view.Refresh(); window.UpdateLayout(); StaTest.Pump();
            Assert.DoesNotContain(Nodes(window).OfType<TextBlock>(),t => t.Text.Contains("悬停删除"));
            var ring=Assert.Single(Nodes(window).OfType<HoverDeleteRing>()); ring.Progress=0.5; Assert.Equal(Colors.Red,ring.ProgressColor);
            Assert.Contains(Nodes(window).OfType<TextBlock>(),t => t.Text=="×");
        });
    }
    [Fact] public async Task HoverChoicesAreQualifiedAndShareTheProtectedHwnd()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig=new Rig(); var window=rig.Host.CurrentWindow!; window.Show(); window.UpdateLayout(); StaTest.Pump();
            var selector=Assert.IsType<ModelSelector>(window.FindName("AuxiliaryModels"));
            Assert.True(selector.AuxiliaryInteraction); Assert.Equal(3,selector.ModelChoices.Count);
            Assert.False(selector.ModelChoices.Single(m => m.Model.Name=="unverified").IsEnabled);
            ModelSelection? chosen=null; selector.SelectionRequested+=s => chosen=s;
            selector.OpenHoverMenu(ModelSelectorPart.Model); window.UpdateLayout(); StaTest.Pump(); Assert.True(selector.IsMenuOpen);
            var choice=Nodes(selector).OfType<Button>().First(b => b.Tag is ModelChoice m && m.Model.Name=="kimi-k3");
            Assert.Same(HwndSource.FromHwnd(window.NativeHandle),PresentationSource.FromVisual(choice)); Assert.False(choice.Focusable);
            Assert.Equal(1,DwellBehavior.GetDelay(choice)); DwellBehavior.GetCommand(choice)!.Execute(null); Assert.Equal("kimi-k3",chosen!.ModelName); Assert.False(selector.IsMenuOpen);
            Assert.DoesNotContain(Nodes(selector).OfType<ComboBox>(),c => c.IsVisible);
        });
    }
    [Fact] public async Task AQualifiedNonDefaultModelCanBeExplicitlyChosenWithoutSelectingAnUnverifiedDefault()
    {
        await StaTest.RunAsync(() =>
        {
            var proof=new ServiceVerification(new byte[32]);var a=new AiModelProfile { Name="a" };var blocked=new AiModelProfile { Name="blocked" };var allowed=new AiModelProfile { Name="allowed" };
            var endpoint=new AiSettings { BaseUri=new("https://example.invalid/v1"),SecretFingerprint="digest" };a=a with { TextVerifiedFingerprint=proof.AiFingerprint(new() { Ai=endpoint with { Model=a.Name } }),VerifiedFingerprint=proof.AiFingerprint(new() { Ai=endpoint with { Model=a.Name } }) };allowed=allowed with { TextVerifiedFingerprint=proof.AiFingerprint(new() { Ai=endpoint with { Model=allowed.Name } }),VerifiedFingerprint=proof.AiFingerprint(new() { Ai=endpoint with { Model=allowed.Name } }) };
            var pa=new AiServiceProfile { Name="A",Settings=endpoint,Models=[a],DefaultModelId=a.Id };var pb=new AiServiceProfile { Name="B",Settings=endpoint,Models=[blocked,allowed],DefaultModelId=blocked.Id };
            var settings=AiProfileCatalog.Normalize(new() { AiProfiles=[pa,pb],ActiveAiProfileId=pa.Id });var selector=new ModelSelector { AuxiliaryInteraction=true };ModelSelection? request=null;
            selector.SelectionRequested+=choice=>request=choice;selector.Configure(settings,ModelCatalog.Select(pa,a),true,proof);
            var window=new Window { Content=selector,Width=480,Height=360 };try {
                window.Show();StaTest.Pump();selector.OpenHoverMenu(ModelSelectorPart.Profile);StaTest.Pump();
                var b=Nodes(selector).OfType<Button>().Single(x=>x.Tag is AiServiceProfile p && p.Id==pb.Id);DwellBehavior.GetCommand(b)!.Execute(null);StaTest.Pump();
                Assert.Null(request);Assert.True(selector.IsMenuOpen);
                var model=Nodes(selector).OfType<Button>().Single(x=>x.Tag is ModelChoice m && m.Model.Id==allowed.Id);Assert.False(Nodes(selector).OfType<Button>().Single(x=>x.Tag is ModelChoice m && m.Model.Id==blocked.Id).IsEnabled);
                DwellBehavior.GetCommand(model)!.Execute(null);Assert.Equal(pb.Id,request!.ProfileId);Assert.Equal(allowed.Id,request.ModelId);Assert.False(selector.IsMenuOpen);
            } finally { window.Close(); }
        });
    }
    [Fact] public async Task NewAuxiliaryConversationUsesANewWorkspaceRuntime()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig=new Rig(); var old=rig.Coordinator.State.Id; var window=rig.Host.CurrentWindow!; window.Show(); StaTest.Pump();
            DwellBehavior.GetCommand(Assert.IsType<Button>(window.FindName("NewButton")))!.Execute(null);
            var timer=System.Diagnostics.Stopwatch.StartNew(); while(rig.Workspace.AuxiliarySessionId==old && timer.Elapsed<TimeSpan.FromSeconds(3)) { StaTest.Pump(); Thread.Sleep(5); }
            Assert.NotEqual(old,rig.Workspace.AuxiliarySessionId); Assert.False(rig.Workspace.Find(old)!.State.IsAuxiliaryMode);
            Assert.Equal(rig.Workspace.AuxiliarySessionId,Assert.IsType<OverlayViewModel>(rig.Host.CurrentWindow!.DataContext).SessionId);
        });
    }
    [Fact] public async Task ClosingTheOverlayViewAfterReturnDoesNotCancelItsAnswer()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig=new Rig(); var window=rig.Host.CurrentWindow!; window.Show(); StaTest.Pump();
            rig.Coordinator.UpdateDraftTextAsync("question",default).GetAwaiter().GetResult(); var answer=rig.Ai.QueueAttempt();
            var view=Assert.IsType<OverlayViewModel>(window.DataContext); view.Command(AppCommand.Send).Execute(null); Wait(() => answer.Started.Task.IsCompleted);
            rig.Coordinator.ReturnHomeAsync(default).GetAwaiter().GetResult(); Wait(() => rig.Host.CurrentWindow is null);
            answer.Text("background answer"); answer.Complete(); rig.Coordinator.WaitForIdleAsync(default).GetAwaiter().GetResult();
            Assert.Equal(MessageStatus.Complete,rig.Coordinator.State.Messages[^1].Status); Assert.Equal("background answer",rig.Coordinator.State.Messages[^1].Text);
        });
    }
    [Fact] public async Task ClosingTheOverlayViewAfterReturnDoesNotCancelRecognition()
    {
        await StaTest.RunAsync(() =>
        {
            var asr=new PendingAsr(); using var rig=new Rig(asr); var window=rig.Host.CurrentWindow!; window.Show(); StaTest.Pump();
            Assert.IsType<OverlayViewModel>(window.DataContext).Command(AppCommand.ToggleRecording).Execute(null); Wait(() => rig.Coordinator.State.SpeechState==SpeechState.Recording);
            rig.Coordinator.ReturnHomeAsync(default).GetAwaiter().GetResult(); Wait(() => rig.Host.CurrentWindow is null && asr.Started.Task.IsCompleted);
            asr.Done.TrySetResult("voice after return"); Wait(() => rig.Coordinator.State.SpeechState!=SpeechState.Recognizing);
            Assert.Equal("voice after return",rig.Coordinator.State.Draft.Text);
        });
    }
    private static void Wait(Func<bool> ready) { var timer=System.Diagnostics.Stopwatch.StartNew(); while(!ready() && timer.Elapsed<TimeSpan.FromSeconds(3)) { StaTest.Pump(); Thread.Sleep(5); } Assert.True(ready()); }
    private sealed class PendingAsr : ExamHelper.Core.Audio.IAsrClient
    {
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource<string> Done { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranscribeAsync(ExamHelper.Core.Audio.RecordedAudio audio,AsrSettings settings,CancellationToken ct) { Started.TrySetResult(); return Done.Task.WaitAsync(ct); }
    }
    private static IEnumerable<DependencyObject> Nodes(DependencyObject root) => MarkdownViewTests.Nodes(root);
    private sealed class Rig : IDisposable
    {
        public DeferredAiClient Ai { get; }=new();
        public ServiceVerification Verification { get; }=new(new byte[32]);
        public SessionCoordinator Coordinator { get; }
        public ConversationWorkspace Workspace { get; }
        public OverlayHost Host { get; }
        public Rig(ExamHelper.Core.Audio.IAsrClient? asr = null)
        {
            var stub=new StubServices(); var store=new InMemorySessionStore();
            var a=new AiModelProfile { Name="model-a" }; var b=new AiModelProfile { Name="unverified" }; var c=new AiModelProfile { Name="kimi-k3" };
            var endpoint=new AiSettings { BaseUri=new("https://example.invalid/v1"),Model=a.Name,SecretFingerprint="digest" };
            a=a with { TextVerifiedFingerprint=Verification.AiFingerprint(new() { Ai=endpoint }),VerifiedFingerprint=Verification.AiFingerprint(new() { Ai=endpoint }) }; c=c with { TextVerifiedFingerprint=Verification.AiFingerprint(new() { Ai=endpoint with { Model=c.Name } }),VerifiedFingerprint=Verification.AiFingerprint(new() { Ai=endpoint with { Model=c.Name } }) };
            var p=new AiServiceProfile { Name="测试配置",Models=[a,b,c],DefaultModelId=a.Id,Settings=endpoint };
            var settings=AiProfileCatalog.Normalize(new() { AiProfiles=[p],ActiveAiProfileId=p.Id,Asr=new() { SecretFingerprint=Verification.SecretDigest("asr") } });
            settings=Verification.RecordAsrSuccess(settings,Verification.AsrFingerprint(settings));
            Workspace=new(store,() => new SessionCoordinator(Ai,asr ?? stub,new StubServices(),stub,stub,() => settings,store,new ManualTimeProvider(),verification:Verification));
            Coordinator=Workspace.NewAsync(true,default).GetAwaiter().GetResult();
            Host=new(Coordinator,stub,() => settings,_ => Task.CompletedTask,() => Workspace.ReturnHomeAsync(CancellationToken.None),() => Task.CompletedTask,new Protection(),new Keys(),workspace: Workspace,verification: Verification);
            Host.OpenAsync(default).GetAwaiter().GetResult();
        }
        public void Dispose() { Host.Close(); Workspace.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    private sealed class Protection : IWindowProtectionApi
    { public bool Set(nint h,uint a,out int e) { e=0; return true; } public bool Get(nint h,out uint a,out int e) { a=0x11; e=0; return true; } }
    private sealed class Keys : IHotkeyApi
    { public bool Register(nint h,int i,uint m,uint k,out int e) { e=0; return true; } public bool Unregister(nint h,int i) => true; }
}
