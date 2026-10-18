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
    [Fact] public void LateLeaseDisposalCannotReleaseNewGeneration()
    {
        var gate = new AiGenerationGate(); var first = gate.TryAcquire(Guid.NewGuid())!; first.Dispose();
        var nextId = Guid.NewGuid(); using var second = gate.TryAcquire(nextId)!;
        first.Dispose(); Assert.True(gate.IsBusy); Assert.Equal(nextId,gate.OwnerId); Assert.Null(gate.TryAcquire(Guid.NewGuid()));
    }
}
