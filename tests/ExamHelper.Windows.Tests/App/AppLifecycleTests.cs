using ExamHelper.App.Home;
using ExamHelper.App.Lifecycle;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class AppLifecycleTests
{
    [Fact] public async Task FailedExitKeepsTrayAndDraftsThenRetrySavesPartialAnswerExactlyOnce()
    {
        await using var rig=new Rig(); var a=await rig.Runtime.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("question",default); var answer=rig.Runtime.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task;
        answer.Text("partial"); await Wait(() => a.State.Messages[^1].Text=="partial"); await a.UpdateDraftTextAsync("A draft",default);
        var b=await rig.Runtime.Workspace.NewAsync(false,default); await b.UpdateDraftTextAsync("B draft",default);
        rig.Runtime.Store.FailWrites=true;
        Assert.False(await rig.Lifecycle.RequestExitAsync(default)); Assert.True(rig.Tray.IsVisible); Assert.Equal(0,rig.Shutdowns);
        Assert.Equal(MessageStatus.Stopped,a.State.Messages[^1].Status); Assert.Equal("A draft",a.State.Draft.Text); Assert.Equal("B draft",b.State.Draft.Text);
        rig.Runtime.Store.FailWrites=false;
        Assert.True(await rig.Lifecycle.RequestExitAsync(default)); Assert.True(await rig.Lifecycle.RequestExitAsync(default)); Assert.Equal(1,rig.Shutdowns); Assert.Equal(1,rig.Tray.DisposeCount);
        Assert.Equal("partial",(await rig.Runtime.Store.LoadAsync(a.State.Id,default))!.Messages[^1].Text); Assert.Equal("B draft",(await rig.Runtime.Store.LoadAsync(b.State.Id,default))!.Draft.Text);
    }
    [Fact] public async Task TrayActivationReturnsFromAuxiliaryAndRestoresItsDraft()
    {
        await using var rig=new Rig(); var a=await rig.Runtime.Workspace.NewAsync(true,default); await a.UpdateDraftTextAsync("auxiliary draft",default); rig.Home.HideHome();
        await rig.Lifecycle.ShowHomeAsync(default);
        Assert.True(rig.Home.HomeVisible); Assert.Null(rig.Runtime.Workspace.AuxiliarySessionId); Assert.Same(a,rig.Runtime.Workspace.Selected); Assert.Equal("auxiliary draft",rig.Home.DraftText);
    }
    [Fact] public async Task CancellingDirtySettingsCloseDoesNotStopTheAnswerOrExit()
    {
        await using var rig=new Rig(() => Task.FromResult(false)); var a=await rig.Runtime.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("q",default);
        var answer=rig.Runtime.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task;
        Assert.False(await rig.Lifecycle.RequestExitAsync(default)); Assert.Equal(AnswerState.Generating,a.State.AnswerState); Assert.Equal(0,rig.Shutdowns); Assert.True(rig.Tray.IsVisible);
        answer.Complete(); await a.WaitForIdleAsync(default);
    }
    [Fact] public async Task MainWindowXOnlyHidesAndSingleTrayActivationRestoresIt()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new Rig(); rig.Runtime.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            var window=new HomeWindow(rig.Home,() => throw new NotSupportedException(),() => Task.CompletedTask); window.AttachLifecycle(rig.Lifecycle);
            try
            {
                window.Show(); StaTest.Pump(); window.Close(); StaTest.Pump(); Assert.False(window.IsVisible); Assert.True(rig.Tray.IsVisible); Assert.Equal(0,rig.Shutdowns);
                rig.Tray.Activate(); var watch=System.Diagnostics.Stopwatch.StartNew(); while(!window.IsVisible && watch.Elapsed<TimeSpan.FromSeconds(2)) { StaTest.Pump(); Thread.Sleep(5); } Assert.True(window.IsVisible);
            }
            finally { rig.Lifecycle.RequestExitAsync(default).GetAwaiter().GetResult(); window.Close(); StaTest.Pump(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact] public void DoubleClickDebounceAcceptsOnlyOneActivationPerGesture()
    {
        var clock=new ManualTimeProvider(); var clicks=new TrayActivationDebounce(clock,TimeSpan.FromMilliseconds(500));
        Assert.True(clicks.TryActivate()); clock.Advance(TimeSpan.FromMilliseconds(200)); Assert.False(clicks.TryActivate()); clock.Advance(TimeSpan.FromMilliseconds(500)); Assert.True(clicks.TryActivate());
    }
    [Fact] public async Task ExitWaitsForCapturedImportsAfterCancellingAiAndSavesTheResultingDraft()
    {
        await using var rig=new Rig(); var a=await rig.Runtime.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("q",default);
        var answer=rig.Runtime.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task;
        var importer=new Importer(); var editor=new HomeDraftController(() => rig.Runtime.Workspace.Selected,importer); var upload=editor.ImportFilesAsync(["owned.png"],default); await importer.Started.Task;
        using var tray=new Tray(); var shutdowns=0;
        using var lifecycle=new AppLifecycle(rig.Runtime.Workspace,rig.Home,tray,() => { shutdowns++; return Task.CompletedTask; },_ => { },flushEditors: () => editor.DrainAllAsync(default));
        var exit=lifecycle.RequestExitAsync(default); await Task.Delay(20); Assert.False(exit.IsCompleted); Assert.Equal(AnswerState.Idle,a.State.AnswerState);
        importer.Done.TrySetResult(); await upload; Assert.True(await exit); Assert.Equal(1,shutdowns); Assert.Single((await rig.Runtime.Store.LoadAsync(a.State.Id,default))!.Draft.Images);
    }
    [Fact] public async Task UsageWindowIncludesTheAgreedDisclaimerAndCurrentKeyboardRules()
    {
        await StaTest.RunAsync(() =>
        {
            var help=new ExamHelper.App.Help.UsageWindow(ExamHelper.Core.Settings.HotkeyBinding.Defaults);
            help.Show(); help.UpdateLayout(); StaTest.Pump();
            try { Assert.Contains("Enter 发送",help.Instructions); Assert.Contains("Shift + Enter",help.Instructions); Assert.Contains("仅供学习",help.Instructions); Assert.Contains("禁止使用外部辅助工具",help.Instructions); Assert.Contains("HTML",help.Instructions); }
            finally { help.Close(); }
        });
    }
    private sealed class Importer : ExamHelper.Windows.Images.IImageImporter
    {
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Done { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<IReadOnlyList<ExamHelper.Core.Drafts.ImageRef>> ImportFilesAsync(IEnumerable<string> files,CancellationToken ct) { Started.TrySetResult(); await Done.Task.WaitAsync(ct); return [new(Guid.NewGuid(),"owned.png",12,12)]; }
        public Task<IReadOnlyList<ExamHelper.Core.Drafts.ImageRef>> ImportBitmapAsync(System.Windows.Media.Imaging.BitmapSource bitmap,CancellationToken ct) => throw new NotSupportedException();
    }
    private static async Task Wait(Func<bool> ready) { using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(3)); while(!ready()) await Task.Delay(5,limit.Token); }
    private sealed class Rig : IAsyncDisposable
    {
        public ConversationWorkspaceRig Runtime { get; }=new(); public Tray Tray { get; }=new(); public HomeViewModel Home { get; } public AppLifecycle Lifecycle { get; } public int Shutdowns;
        public Rig(Func<Task<bool>>? closeSettings = null)
        {
            var host=new Host(); Home=new(Runtime.Workspace,Runtime.Store,new StubServices(),() => Task.FromResult(Runtime.Settings),new(new byte[32]),host,() => Task.CompletedTask);
            Lifecycle=new(Runtime.Workspace,Home,Tray,() => { Shutdowns++; return Task.CompletedTask; },s => Home.Status=s,closeSettings);
        }
        public async ValueTask DisposeAsync() { Lifecycle.Dispose(); await Runtime.DisposeAsync(); }
    }
    private sealed class Tray : ITrayIconService
    {
        public bool IsVisible => DisposeCount==0; public int DisposeCount; public event Action? Activated; public event Action? ExitRequested;
        public void Activate() => Activated?.Invoke(); public void Exit() => ExitRequested?.Invoke(); public void Dispose() { DisposeCount++; }
    }
    private sealed class Host : IOverlayHost
    { public Task<ExamHelper.Windows.Native.ProtectionResult> OpenAsync(CancellationToken ct) => throw new NotSupportedException(); public void Show() { } public void Close() { } }
}
