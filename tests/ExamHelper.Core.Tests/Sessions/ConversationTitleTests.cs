using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public class ConversationTitleTests
{
    [Fact] public void UsesFirstSendTimeAndHonorsManualTitle()
    {
        var time = new DateTimeOffset(2026,10,18,10,11,12,TimeSpan.FromHours(8));
        var session = new SessionSnapshot { Messages = [new() { Role = MessageRole.User,Text = "not a title",CreatedAt = time }] };
        Assert.Equal("对话 "+time.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),ConversationTitle.Get(session));
        Assert.Equal("对话（未发送）",ConversationTitle.Get(new()));
        Assert.Equal("My title",ConversationTitle.Get(session with { CustomTitle = "My title" }));
    }
    [Fact] public async Task DebounceKeepsLatestDraftAndSwitchForcesFlush()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("draft",default);
        Assert.False((await rig.Store.LoadAsync(a.State.Id,default))?.Draft.HasContent ?? false);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(499)); Assert.False((await rig.Store.LoadAsync(a.State.Id,default))?.Draft.HasContent ?? false);
        rig.Clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.Equal("draft",(await rig.Store.LoadAsync(a.State.Id,default))!.Draft.Text);
        await a.UpdateDraftTextAsync("next",default); await rig.Workspace.NewAsync(false,default);
        Assert.Equal("next",(await rig.Store.LoadAsync(a.State.Id,default))!.Draft.Text);
    }
    [Fact] public async Task StreamCannotRollBackTitleOrFirstAttemptTime()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(false,default);
        await a.UpdateDraftTextAsync("q",default); var attempt = rig.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        var first = a.State.FirstSentAt; await a.RenameAsync("renamed",default); rig.Clock.Advance(TimeSpan.FromSeconds(1));
        attempt.Text("answer"); attempt.Complete(); await a.WaitForIdleAsync(default);
        Assert.Equal(first,a.State.FirstSentAt); Assert.Equal("renamed",ConversationTitle.Get((await rig.Store.LoadAsync(a.State.Id,default))!));
    }
}
