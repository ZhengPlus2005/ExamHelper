using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public class ConversationWorkspaceTests
{
    [Fact] public async Task BackgroundAnswerNeverWritesSelectedConversationAndBusySendDoesNotQueue()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("A question",default); var answer = rig.Ai.QueueAttempt();
        await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task;
        var b = await rig.Workspace.NewAsync(false,default); await b.UpdateDraftTextAsync("B draft",default);
        Assert.Equal(a.State.Id,rig.Workspace.GeneratingSessionId); Assert.False(b.CanExecute(AppCommand.Send));
        await Assert.ThrowsAsync<InvalidOperationException>(() => b.ExecuteAsync(AppCommand.Send,default));
        answer.Text("A answer"); answer.Complete(); await a.WaitForIdleAsync(default);
        Assert.Equal("A answer",a.State.Messages[^1].Text); Assert.Empty(b.State.Messages); Assert.Equal("B draft",b.State.Draft.Text);
        Assert.True(b.CanExecute(AppCommand.Send));
        await rig.Workspace.OpenAsync(a.State.Id,default); Assert.Same(a,rig.Workspace.Selected);
        await rig.Workspace.OpenAsync(b.State.Id,default); Assert.Equal("B draft",rig.Workspace.Selected!.State.Draft.Text);
    }
    [Fact] public async Task FailedFlushDoesNotSwitchAwayOrLoseDraft()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("keep",default); rig.Store.FailWrites = true;
        await Assert.ThrowsAnyAsync<Exception>(() => rig.Workspace.NewAsync(false,default));
        Assert.Same(a,rig.Workspace.Selected); Assert.Equal("keep",a.State.Draft.Text);
        rig.Store.FailWrites = false;
    }
    [Fact] public async Task DisposedRuntimeCannotRecreateDeletedHistoryThroughALateFlush()
    {
        await using var rig=new ConversationWorkspaceRig(); var a=await rig.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("delete",default); await a.FlushAsync(default);
        await rig.Workspace.DeleteAsync(a.State.Id,default);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => a.FlushAsync(default)); Assert.Null(await rig.Store.LoadAsync(a.State.Id,default));
    }
    [Fact] public async Task ExitSuspensionRejectsLateSendWithoutConsumingItsDraft()
    {
        await using var rig=new ConversationWorkspaceRig(); var a=await rig.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("keep",default);
        var answer=rig.Ai.QueueAttempt(); rig.CommandsSuspended=true;
        Assert.False(a.CanExecute(AppCommand.Send)); await Assert.ThrowsAsync<InvalidOperationException>(() => a.ExecuteAsync(AppCommand.Send,default));
        Assert.Equal("keep",a.State.Draft.Text); Assert.False(answer.Started.Task.IsCompleted);
        rig.CommandsSuspended=false; await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task; answer.Complete(); await a.WaitForIdleAsync(default);
    }
    [Fact] public async Task ExitWaitsForNavigationAlreadyLoadingAnotherConversation()
    {
        var store=new LoadingStore(); var stub=new StubServices(); var snapshot=new SessionSnapshot { Draft=new("B",[],1) }; await store.Inner.SaveAsync(snapshot,default);
        store.DelayedId=snapshot.Id;
        await using var workspace=new ConversationWorkspace(store,() => new SessionCoordinator(stub,stub,new StubServices(),stub,stub,() => new(),store,new ManualTimeProvider()));
        await workspace.NewAsync(false,default); var opening=workspace.OpenAsync(snapshot.Id,default); await store.Started.Task;
        var exit=workspace.ExitAsync(default); var prematurelyCompleted=exit.IsCompleted; store.Release.TrySetResult(); await opening; await exit;
        Assert.False(prematurelyCompleted); Assert.Equal("B",(await store.Inner.LoadAsync(snapshot.Id,default))!.Draft.Text);
    }
    [Fact] public async Task FailedHistoryDeletionKeepsTheRuntimeEditableAndRetryable()
    {
        var store=new DeleteFailingStore();var stub=new StubServices();
        await using var workspace=new ConversationWorkspace(store,()=>new SessionCoordinator(stub,stub,new StubServices(),stub,stub,()=>new(),store,new ManualTimeProvider()));
        var a=await workspace.NewAsync(false,default);await a.UpdateDraftTextAsync("keep",default);
        await Assert.ThrowsAsync<IOException>(()=>workspace.DeleteAsync(a.State.Id,default));
        Assert.Same(a,workspace.Selected);Assert.Same(a,workspace.Find(a.State.Id));
        await a.UpdateDraftTextAsync("edited after failure",default);await a.FlushAsync(default);
        var b=await workspace.NewAsync(false,default);await workspace.OpenAsync(a.State.Id,default);Assert.Equal("edited after failure",workspace.Selected!.State.Draft.Text);
        store.Fail=false;await workspace.DeleteAsync(a.State.Id,default);Assert.Null(await store.LoadAsync(a.State.Id,default));
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>a.FlushAsync(default));await workspace.OpenAsync(b.State.Id,default);
    }
    private sealed class DeleteFailingStore : ISessionStore
    {
        private readonly InMemorySessionStore _inner=new();public bool Fail=true;
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct)=>_inner.SaveAsync(s,ct);public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)=>_inner.LoadAsync(id,ct);
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)=>_inner.ListAsync(ct);
        public Task DeleteAsync(Guid id,CancellationToken ct)=>Fail ? Task.FromException(new IOException("delete failed")) : _inner.DeleteAsync(id,ct);
    }
    private sealed class LoadingStore : ISessionStore
    {
        public InMemorySessionStore Inner { get; }=new(); public Guid DelayedId; public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SaveAsync(SessionSnapshot state,CancellationToken ct) => Inner.SaveAsync(state,ct);
        public async Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct) { if(id==DelayedId) { Started.TrySetResult(); await Release.Task.WaitAsync(ct); } return await Inner.LoadAsync(id,ct); }
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct) => Inner.ListAsync(ct); public Task DeleteAsync(Guid id,CancellationToken ct) => Inner.DeleteAsync(id,ct);
    }
    [Fact] public void LateLeaseDisposalCannotReleaseNewGeneration()
    {
        var gate = new AiGenerationGate(); var first = gate.TryAcquire(Guid.NewGuid())!; first.Dispose();
        var nextId = Guid.NewGuid(); using var second = gate.TryAcquire(nextId)!;
        first.Dispose(); Assert.True(gate.IsBusy); Assert.Equal(nextId,gate.OwnerId); Assert.Null(gate.TryAcquire(Guid.NewGuid()));
    }
}
