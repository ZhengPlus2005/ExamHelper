using ExamHelper.Core.Drafts;
using Xunit;
namespace ExamHelper.Core.Tests.Drafts;

public class DraftBufferTests
{
    private static ImageRef Image() => new(Guid.NewGuid(), Guid.NewGuid() + ".png", 100, 80);
    [Fact]
    public void PreservesImageOrderAndSnapshotDoesNotChange()
    {
        var buffer = new DraftBuffer(); var a = Image(); var b = Image();
        buffer.AppendImage(a); var first = buffer.Snapshot(); buffer.AppendImage(b);
        Assert.Equal(new[] { a, b }, buffer.Snapshot().Images);
        Assert.Single(first.Images);
        Assert.Equal(2, buffer.Snapshot().Revision);
        buffer.RemoveImage(a.Id); Assert.Equal(b, Assert.Single(buffer.Snapshot().Images));
    }
    [Fact]
    public void ClearRemovesTextAndImagesOnly()
    {
        var buffer = new DraftBuffer(); buffer.AppendText("第一题"); buffer.AppendImage(Image());
        var saved = buffer.Snapshot(); buffer.Clear();
        Assert.False(buffer.Snapshot().HasContent);
        Assert.Equal("第一题", saved.Text); Assert.Single(saved.Images);
        Assert.Equal(saved.Revision + 1, buffer.Snapshot().Revision);
    }
    [Fact]
    public void WhitespaceIsDraftAndEachMutationIncrementsRevision()
    {
        var buffer = new DraftBuffer(); buffer.AppendText(" ");
        Assert.True(buffer.Snapshot().HasContent); Assert.Equal(1, buffer.Snapshot().Revision);
        buffer.AppendText("hello"); Assert.Equal(" hello", buffer.Snapshot().Text);
        Assert.Equal(2, buffer.Snapshot().Revision);
    }
}