using ExamHelper.Core.Interaction;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public sealed class BatchDelete04Tests
{
    [Fact]
    public async Task PartialFailureKeepsFailedRuntimeEditableAndDoesNotRollBackDeletedItems()
    {
        await using var rig=new Rig(); var a=await rig.New("A"); var b=await rig.New("B"); var c=await rig.New("C"); rig.Store.FailingIds.Add(b.State.Id);
        var result=await rig.Workspace.DeleteManyAsync([a.State.Id,b.State.Id,a.State.Id],default);
        Assert.Equal(new[] { a.State.Id },result.DeletedIds); Assert.Contains(b.State.Id,result.Failures.Keys);
        Assert.Null(await rig.Store.LoadAsync(a.State.Id,default)); Assert.Same(b,rig.Workspace.Find(b.State.Id)); Assert.Same(c,rig.Workspace.Selected);
        await b.UpdateDraftTextAsync("editable after failure",default); await b.FlushAsync(default); Assert.Equal("editable after failure",(await rig.Store.LoadAsync(b.State.Id,default))!.Draft.Text);
        rig.Store.FailingIds.Clear(); var retry=await rig.Workspace.DeleteManyAsync([b.State.Id],default);
        Assert.Empty(retry.Failures); Assert.Null(await rig.Store.LoadAsync(b.State.Id,default)); Assert.NotNull(await rig.Store.LoadAsync(c.State.Id,default));
    }
    [Fact]
    public async Task DeletingOtherConversationDoesNotStopActiveAnswer()
    {
        await using var rig=new Rig(); var a=await rig.New("A"); var attempt=rig.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        var b=await rig.New("B"); await rig.Workspace.DeleteManyAsync([b.State.Id],default);
        Assert.Equal(AnswerState.Generating,a.State.AnswerState); Assert.Equal(a.State.Id,rig.Workspace.GeneratingSessionId);
        attempt.Complete(); await a.WaitForIdleAsync(default);
    }
    [Fact]
    public async Task DeletedRequestLateChunkCannotRecreateHistory()
    {
        await using var rig=new Rig(); var a=await rig.New("A"); var attempt=rig.Ai.QueueAttempt(true); await a.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        var old=a.WaitForIdleAsync(default); var result=await rig.Workspace.DeleteManyAsync([a.State.Id],default);
        attempt.Text("late"); attempt.Complete(); await old.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Contains(a.State.Id,result.DeletedIds); Assert.Null(rig.Workspace.Find(a.State.Id)); Assert.Null(await rig.Store.LoadAsync(a.State.Id,default));
    }
    [Fact]
    public async Task CancellationPreservesDeletedResultsAndMarksRemainingTargetsRetryable()
    {
        await using var rig=new Rig(); var a=await rig.New("A"); var b=await rig.New("B"); var c=await rig.New("C"); using var cancel=new CancellationTokenSource();
        rig.Store.OnDeleted=id=> { if(id==a.State.Id) cancel.Cancel(); };
        var result=await rig.Workspace.DeleteManyAsync([a.State.Id,b.State.Id,c.State.Id],cancel.Token);
        Assert.Equal(new[] { a.State.Id },result.DeletedIds); Assert.Contains(b.State.Id,result.Failures.Keys); Assert.Contains(c.State.Id,result.Failures.Keys);
        Assert.NotNull(await rig.Store.LoadAsync(b.State.Id,default)); Assert.NotNull(await rig.Store.LoadAsync(c.State.Id,default));
    }
    [Fact]
    public async Task PendingOldLoadCannotReattachConversationAfterDeletion()
    {
        await using var rig=new Rig(); await rig.New("A"); var old=new SessionSnapshot { Draft=new("B",[],1) }; await rig.Store.SaveAsync(old,default);
        rig.Store.DelayedId=old.Id; var opening=rig.Workspace.OpenAsync(old.Id,default); await rig.Store.LoadStarted.Task;
        var deleting=rig.Workspace.DeleteManyAsync([old.Id],default); Assert.False(deleting.IsCompleted);
        rig.Store.LoadRelease.TrySetResult(); var loaded=await opening; var result=await deleting;
        Assert.Contains(old.Id,result.DeletedIds); Assert.Null(rig.Workspace.Find(old.Id)); Assert.Null(await rig.Store.LoadAsync(old.Id,default));
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>loaded.FlushAsync(default));
    }
    [Fact]
    public async Task PostDeleteResourceFailureIsNotReportedAsAnUndeletedHistoryItem()
    {
        var store=new InMemorySessionStore();var stub=new StubServices();var settings=TestSettings.Configured();var microphone=new CleanupFailingMicrophone();
        await using var workspace=new ConversationWorkspace(store,()=>new SessionCoordinator(stub,stub,microphone,stub,stub,()=>settings,store,new ManualTimeProvider()));
        var runtime=await workspace.NewAsync(false,default);await runtime.UpdateDraftTextAsync("saved",default);await runtime.FlushAsync(default);
        var result=await workspace.DeleteManyAsync([runtime.State.Id],default);
        Assert.Contains(runtime.State.Id,result.DeletedIds);Assert.Empty(result.Failures);Assert.Contains(runtime.State.Id,result.CleanupFailures.Keys);
        Assert.Null(await store.LoadAsync(runtime.State.Id,default));Assert.Null(workspace.Find(runtime.State.Id));
    }
    private sealed class CleanupFailingMicrophone:IMicrophoneRecorder
    {
        private int _cancelCount;
        public Task StartAsync(string id,CancellationToken ct)=>Task.CompletedTask;
        public Task<RecordedAudio> StopAsync(CancellationToken ct)=>Task.FromResult(new RecordedAudio(Guid.NewGuid(),"owned-test.wav",TimeSpan.FromSeconds(1)));
        public Task CancelAsync()=>++_cancelCount==2 ? Task.FromException(new IOException("resource release failed")) : Task.CompletedTask;
    }
    private sealed class Rig:IAsyncDisposable
    {
        public FailingStore Store { get; }=new(); public DeferredAiClient Ai { get; }=new(); public ManualTimeProvider Clock { get; }=new();
        public ConversationWorkspace Workspace { get; }
        public Rig()
        {
            var proof=new ServiceVerification(new byte[32]); var settings=TestSettings.Qualified(proof); var stub=new StubServices(); var generation=new AiGenerationGate();
            Workspace=new(Store,()=>new SessionCoordinator(Ai,stub,stub,stub,stub,()=>settings,Store,Clock,generation,proof));
        }
        public async Task<SessionCoordinator> New(string text)
        { var runtime=await Workspace.NewAsync(false,default); await runtime.UpdateDraftTextAsync(text,default); await runtime.FlushAsync(default); Clock.Advance(TimeSpan.FromMinutes(1));return runtime; }
        public ValueTask DisposeAsync()=>Workspace.DisposeAsync();
    }
    private sealed class FailingStore:ISessionStore
    {
        private readonly InMemorySessionStore _inner=new(); public HashSet<Guid> FailingIds { get; }=[]; public Action<Guid>? OnDeleted;
        public Guid? DelayedId;public TaskCompletionSource LoadStarted { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource LoadRelease { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct)=>_inner.SaveAsync(s,ct);
        public async Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)
        { if(id==DelayedId) { LoadStarted.TrySetResult();await LoadRelease.Task.WaitAsync(ct); } return await _inner.LoadAsync(id,ct); }
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)=>_inner.ListAsync(ct);
        public async Task DeleteAsync(Guid id,CancellationToken ct)
        { if(FailingIds.Contains(id)) throw new IOException("cannot delete this item"); await _inner.DeleteAsync(id,ct); OnDeleted?.Invoke(id); }
    }
}
