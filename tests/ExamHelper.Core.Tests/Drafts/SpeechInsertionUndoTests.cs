using ExamHelper.Core.Drafts;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Drafts;
public class SpeechInsertionUndoTests
{
    private readonly ManualTimeProvider _clock = new();
    private readonly DraftBuffer _draft = new();
    [Fact]
    public void UndoOnlyLatestInsertionKeepsEarlierTextAndImages()
    {
        _draft.AppendText("原文"); var image = new ImageRef(Guid.NewGuid(), "image.png", 10, 10);
        _draft.AppendImage(image); using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "结果");
        Assert.Equal("原文" + Environment.NewLine + "结果", _draft.Snapshot().Text);
        Assert.True(undo.TryUndo()); Assert.Equal("原文", _draft.Snapshot().Text);
        Assert.Equal(image, Assert.Single(_draft.Snapshot().Images)); Assert.False(undo.TryUndo());
    }
    [Fact]
    public void ExpiresAt3000ms()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock); undo.Append(Guid.NewGuid(), "结果");
        _clock.Advance(TimeSpan.FromMilliseconds(2999)); Assert.True(undo.CanUndo);
        _clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.False(undo.CanUndo);
        Assert.Equal(TimeSpan.Zero, undo.Remaining); Assert.False(undo.TryUndo());
        Assert.Equal("结果", _draft.Snapshot().Text);
    }
    [Fact]
    public void HoverPausesExpiryAndLeavingResumesRemainder()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock); undo.Append(Guid.NewGuid(), "结果");
        _clock.Advance(TimeSpan.FromSeconds(2)); undo.PointerEnter();
        _clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(undo.CanUndo); Assert.Equal(TimeSpan.FromSeconds(1), undo.Remaining);
        undo.PointerLeave(); _clock.Advance(TimeSpan.FromMilliseconds(999)); Assert.True(undo.CanUndo);
        _clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.False(undo.CanUndo);
    }
    [Fact]
    public void NewRecognitionReplacesUndoTargetAndResetsExpiry()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "第一"); _clock.Advance(TimeSpan.FromSeconds(2));
        undo.Append(Guid.NewGuid(), "第二"); _clock.Advance(TimeSpan.FromSeconds(2));
        Assert.True(undo.TryUndo()); Assert.Equal("第一", _draft.Snapshot().Text);
    }
    [Fact]
    public void ClearInvalidatesUndoEvenIfSameTextIsAppendedAgain()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "结果"); _draft.Clear(); _draft.AppendText("结果");
        Assert.False(undo.TryUndo()); Assert.Equal("结果", _draft.Snapshot().Text);
    }
    [Fact]
    public void ImagesDoNotInvalidateUndoAndSurrogatePairsStayIntact()
    {
        _draft.AppendText("保留😀\n"); using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "新增𠀀");
        _draft.AppendImage(new(Guid.NewGuid(), "image.png", 10, 10));
        Assert.True(undo.TryUndo()); Assert.Equal("保留😀\n", _draft.Snapshot().Text);
        Assert.Single(_draft.Snapshot().Images);
    }
    [Fact]
    public void ExternalTextAppendOrRestorationCannotBeRolledBack()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "第一"); _draft.AppendText("新内容");
        Assert.False(undo.TryUndo()); Assert.Equal("第一新内容", _draft.Snapshot().Text);
        undo.Append(Guid.NewGuid(), "第二"); _draft.Restore(DraftSnapshot.Empty);
        Assert.False(undo.TryUndo());
    }
    [Fact]
    public void ExpiredButtonCannotBeRevivedByHoverAndExplicitInvalidationWorks()
    {
        using var undo = new SpeechInsertionUndo(_draft, _clock);
        undo.Append(Guid.NewGuid(), "结果"); _clock.Advance(TimeSpan.FromSeconds(3));
        undo.PointerEnter(); Assert.False(undo.CanUndo);
        undo.Append(Guid.NewGuid(), "第二"); undo.Invalidate(); Assert.False(undo.CanUndo);
    }
}