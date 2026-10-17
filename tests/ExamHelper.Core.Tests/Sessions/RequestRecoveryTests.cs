using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public class RequestRecoveryTests
{
    [Fact]
    public void FailureRestoresFrozenImagesInOrderOnlyWhenDraftIsEmpty()
    {
        var a = new ImageRef(Guid.NewGuid(),"a.png",1,1); var b = a with { Id = Guid.NewGuid() };
        var request = new RequestSnapshot { Text = "问题",Images = [a,b] };
        var empty = new SessionSnapshot(); var recovered = RequestRecovery.ForFailure(empty,request);
        Assert.Equal(new[] {a,b},recovered.Draft.Images); Assert.Equal("问题",recovered.Draft.Text);
        Assert.Equal(recovered.Draft.Revision,recovered.PendingRequest!.RestoredDraftRevision);
        var filled = empty with { Draft = new("新草稿",[],6) };
        var kept = RequestRecovery.ForFailure(filled,request);
        Assert.Equal(filled.Draft,kept.Draft); Assert.Null(kept.PendingRequest!.RestoredDraftRevision);
    }
}