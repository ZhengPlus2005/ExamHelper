using ExamHelper.Core.Sessions;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Common;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public sealed class ResponseTiming04Tests
{
    [Fact]
    public void TimerUsesMonotonicTimeAndFinishesIdempotently()
    {
        var clock=new ManualTimeProvider(); var id=Guid.NewGuid(); var timer=new ResponseTimer(clock,id,1);
        clock.Advance(TimeSpan.FromSeconds(12.4)); clock.JumpUtc(TimeSpan.FromHours(-2));
        var done=timer.Finish(); Assert.Equal(TimeSpan.FromSeconds(12.4),done.Elapsed); Assert.False(done.IsRunning);
        clock.Advance(TimeSpan.FromSeconds(8)); Assert.Same(done,timer.Finish()); Assert.Equal(TimeSpan.FromSeconds(12.4),timer.Snapshot().Elapsed);
        Assert.Equal(id,done.RequestId); Assert.Equal(1,done.Attempt); Assert.Equal(DateTimeOffset.UnixEpoch,done.StartedAt);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WaitingAndFinalSnapshotsUseTheSameTimerWithoutPublishingTicks(bool auxiliary)
    {
        await using var rig=new ConversationWorkspaceRig(); var runtime=await rig.Workspace.NewAsync(auxiliary,default);
        await runtime.UpdateDraftTextAsync("question",default); var attempt=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        var notifications=0; runtime.StateChanged+=_=>notifications++;
        rig.Clock.Advance(TimeSpan.FromSeconds(12.4)); rig.Clock.JumpUtc(TimeSpan.FromHours(-2));
        var sampled=runtime.SnapshotForPresentation(); var timing=Assert.IsType<ResponseTiming>(sampled.Messages[^1].Timing);
        Assert.Equal(TimeSpan.FromSeconds(12.4),timing.Elapsed); Assert.True(timing.IsRunning);
        rig.Clock.Advance(TimeSpan.FromSeconds(1)); var later=runtime.SnapshotForPresentation();
        Assert.Equal(TimeSpan.FromSeconds(13.4),later.Messages[^1].Timing!.Elapsed); Assert.Equal(TimeSpan.FromSeconds(12.4),timing.Elapsed);
        Assert.Equal(0,notifications);
        attempt.Complete(); await runtime.WaitForIdleAsync(default);
        var final=Assert.IsType<ResponseTiming>(runtime.State.Messages[^1].Timing); Assert.False(final.IsRunning); Assert.Equal(TimeSpan.FromSeconds(13.4),final.Elapsed);
        rig.Clock.Advance(TimeSpan.FromSeconds(90));
        Assert.Equal(TimeSpan.FromSeconds(13.4),(await rig.Store.LoadAsync(runtime.State.Id,default))!.Messages[^1].Timing!.Elapsed);
    }
    [Fact]
    public async Task RetryStartsAtZeroAndPreservesMessageIdentityAndCreationTime()
    {
        await using var rig=new ConversationWorkspaceRig(); var runtime=await rig.Workspace.NewAsync(false,default);
        await runtime.UpdateDraftTextAsync("question",default); var first=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await first.Started.Task; rig.Clock.Advance(TimeSpan.FromSeconds(4));
        first.Fail(new ServiceFailure(FailureCategory.Transport,"failed")); await runtime.WaitForIdleAsync(default);
        var original=runtime.State.Messages[^1]; Assert.Equal(TimeSpan.FromSeconds(4),original.Timing!.Elapsed);
        var second=rig.Ai.QueueAttempt(); await runtime.ExecuteAsync(AppCommand.RetryRequest,default); await second.Started.Task;
        Assert.Equal(TimeSpan.Zero,runtime.SnapshotForPresentation().Messages[^1].Timing!.Elapsed);
        rig.Clock.Advance(TimeSpan.FromSeconds(2.2)); second.Complete(); await runtime.WaitForIdleAsync(default);
        var retried=runtime.State.Messages[^1]; Assert.Equal(original.Id,retried.Id); Assert.Equal(original.CreatedAt,retried.CreatedAt);
        Assert.Equal(original.RequestId,retried.RequestId); Assert.Equal(2,retried.Timing!.Attempt); Assert.Equal(TimeSpan.FromSeconds(2.2),retried.Timing.Elapsed); Assert.Equal(2,runtime.State.Messages.Length);
    }
    [Fact]
    public async Task PreparationLeaseAndPreparationDelayAreIncluded()
    {
        await using var rig=new ConversationWorkspaceRig(); rig.ClientOverride=new PreparingClient(rig.Clock,rig.Ai);
        var runtime=await rig.Workspace.NewAsync(false,default); await runtime.UpdateDraftTextAsync("question",default); var attempt=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        Assert.Equal(TimeSpan.FromSeconds(4.6),runtime.SnapshotForPresentation().Messages[^1].Timing!.Elapsed);
        rig.Clock.Advance(TimeSpan.FromSeconds(2.2)); attempt.Complete(); await runtime.WaitForIdleAsync(default);
        Assert.Equal(TimeSpan.FromSeconds(6.8),runtime.State.Messages[^1].Timing!.Elapsed);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedPreparationKeepsInputWithoutFakeAnswer(bool direct)
    {
        await using var rig=new ConversationWorkspaceRig(); rig.ClientOverride=new PreparingClient(rig.Clock,rig.Ai,true);
        var runtime=await rig.Workspace.NewAsync(direct,default);
        if(direct)
        {
            await runtime.ExecuteAsync(AppCommand.CaptureAndSend,default);
            Assert.Single(runtime.State.Draft.Images);
        }
        else
        {
            await runtime.UpdateDraftTextAsync("question",default);
            await Assert.ThrowsAsync<ServiceFailure>(()=>runtime.ExecuteAsync(AppCommand.Send,default));
            Assert.Equal("question",runtime.State.Draft.Text);
        }
        Assert.Empty(runtime.State.Messages);
    }
    [Fact]
    public async Task StopAndLateCompletionCannotFinishTheNextRequestTimer()
    {
        await using var rig=new ConversationWorkspaceRig(); var runtime=await rig.Workspace.NewAsync(false,default);
        await runtime.UpdateDraftTextAsync("first",default); var first=rig.Ai.QueueAttempt(true);
        await runtime.ExecuteAsync(AppCommand.Send,default); await first.Started.Task; var oldTask=runtime.WaitForIdleAsync(default);
        rig.Clock.Advance(TimeSpan.FromSeconds(5)); await runtime.ExecuteAsync(AppCommand.StopAnswer,default);
        var stopped=runtime.State.Messages[^1];
        await runtime.UpdateDraftTextAsync("second",default); var second=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await second.Started.Task; rig.Clock.Advance(TimeSpan.FromSeconds(2.2));
        first.Text("late"); first.Complete(); await oldTask.WaitAsync(TimeSpan.FromSeconds(2));
        var active=runtime.SnapshotForPresentation(); second.Complete(); await runtime.WaitForIdleAsync(default);
        Assert.Equal(TimeSpan.FromSeconds(5),stopped.Timing!.Elapsed); Assert.False(stopped.Timing.IsRunning);
        Assert.True(active.Messages[^1].Timing!.IsRunning); Assert.Equal(TimeSpan.FromSeconds(2.2),runtime.State.Messages[^1].Timing!.Elapsed);
    }
    [Fact]
    public void RecoveryFreezesStoredElapsedWithoutAddingDowntimeOrInventingTiming()
    {
        var request=Guid.NewGuid(); var elapsed=TimeSpan.FromSeconds(12.4);
        var saved=new SessionSnapshot { Messages=[new() { RequestId=request,Role=MessageRole.Assistant,Timing=new(request,1,DateTimeOffset.UnixEpoch,elapsed,true) },new() { Role=MessageRole.User,Text="old question" }] };
        var recovered=RequestRecovery.RecoverOnStartup(saved);
        Assert.False(recovered.Messages[0].Timing!.IsRunning); Assert.Equal(elapsed,recovered.Messages[0].Timing!.Elapsed); Assert.Null(recovered.Messages[1].Timing);
    }
    [Fact]
    public async Task BackgroundReturnAndExitPreserveAndFreezeTheSameElapsedValue()
    {
        await using var rig=new ConversationWorkspaceRig(); var runtime=await rig.Workspace.NewAsync(true,default);
        await runtime.UpdateDraftTextAsync("question",default); var attempt=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        rig.Clock.Advance(TimeSpan.FromSeconds(2)); await runtime.ExecuteAsync(AppCommand.ToggleVisibility,default); await runtime.ReturnHomeAsync(default);
        rig.Clock.Advance(TimeSpan.FromSeconds(3.4));
        Assert.Equal(TimeSpan.FromSeconds(5.4),runtime.SnapshotForPresentation().Messages[^1].Timing!.Elapsed);
        await rig.Workspace.ExitAsync(default); await runtime.WaitForIdleAsync(default);
        var saved=(await rig.Store.LoadAsync(runtime.State.Id,default))!;
        var json=System.Text.Json.JsonSerializer.Serialize(saved);
        var decoded=System.Text.Json.JsonSerializer.Deserialize<SessionSnapshot>(json)!;
        rig.Clock.Advance(TimeSpan.FromDays(1)); var recovered=RequestRecovery.RecoverOnStartup(decoded);
        Assert.Equal(TimeSpan.FromSeconds(5.4),recovered.Messages[^1].Timing!.Elapsed); Assert.False(recovered.Messages[^1].Timing!.IsRunning);
    }
    [Fact]
    public async Task RunningFlushSavesOnlyItsSampledElapsedAndRecoveryDoesNotAddDowntime()
    {
        await using var rig=new ConversationWorkspaceRig(); var runtime=await rig.Workspace.NewAsync(false,default);
        await runtime.UpdateDraftTextAsync("question",default); var attempt=rig.Ai.QueueAttempt();
        await runtime.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task; rig.Clock.Advance(TimeSpan.FromSeconds(4.2));
        await runtime.FlushAsync(default); var saved=(await rig.Store.LoadAsync(runtime.State.Id,default))!;
        Assert.Equal(TimeSpan.FromSeconds(4.2),saved.Messages[^1].Timing!.Elapsed); Assert.True(saved.Messages[^1].Timing!.IsRunning);
        rig.Clock.Advance(TimeSpan.FromDays(1)); var recovered=RequestRecovery.RecoverOnStartup(saved);
        Assert.Equal(TimeSpan.FromSeconds(4.2),recovered.Messages[^1].Timing!.Elapsed); Assert.False(recovered.Messages[^1].Timing!.IsRunning);
        attempt.Complete(); await runtime.WaitForIdleAsync(default);
    }
    private sealed class PreparingClient(ManualTimeProvider clock,IAiClient inner,bool fail=false):IAiClient
    {
        public Task<IDisposable?> AcquirePreparationAsync(CancellationToken ct) { clock.Advance(TimeSpan.FromSeconds(1.2)); return Task.FromResult<IDisposable?>(null); }
        public Task<IAiClient> PrepareAsync(AiSettings settings,CancellationToken ct)
        { clock.Advance(TimeSpan.FromSeconds(3.4)); return fail ? Task.FromException<IAiClient>(new ServiceFailure(FailureCategory.Authentication,"preparation failed")) : Task.FromResult(inner); }
        public IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request,AiSettings settings,CancellationToken ct)=>inner.StreamAsync(request,settings,ct);
    }
}
