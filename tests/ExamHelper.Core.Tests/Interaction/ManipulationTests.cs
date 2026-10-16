using ExamHelper.Core.Interaction;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Interaction;
public class ManipulationTests
{
    private static PointerSample Sample(double x, double y, int ms) => new(x, y, TimeSpan.FromMilliseconds(ms).Ticks);
    [Fact]
    public void StopsAfter1000msWithin3Dip()
    {
        var c = new ManipulationController(new ManualTimeProvider()); c.Begin(InteractionMode.Moving, Sample(0, 0, 0));
        Assert.False(c.Update(Sample(1, 1, 999)).IsFinished);
        Assert.True(c.Update(Sample(1, 1, 1000)).IsFinished);
    }
    [Fact]
    public void SlowCumulativeMotionResetsStillness()
    {
        var c = new ManipulationController(new ManualTimeProvider()); c.Begin(InteractionMode.Moving, Sample(0, 0, 0));
        c.Update(Sample(2, 0, 400)); c.Update(Sample(4, 0, 800));
        Assert.False(c.Update(Sample(4, 0, 1000)).IsFinished);
        Assert.True(c.Update(Sample(4, 0, 1800)).IsFinished);
    }
    [Fact]
    public void ScrollIgnoresHorizontalDeviation()
    {
        var c = new ManipulationController(new ManualTimeProvider()); c.Begin(InteractionMode.Scrolling, Sample(10, 20, 0));
        var result = c.Update(Sample(600, 70, 400)); Assert.Equal(0, result.DeltaX); Assert.Equal(50, result.DeltaY);
    }
    [Fact]
    public void ReadingAboveBottomDoesNotJump()
    {
        var viewport = new ViewportState(); viewport.Observe(100, 1000, 300); Assert.False(viewport.FollowOutput);
        viewport.Observe(700, 1000, 300); Assert.True(viewport.FollowOutput);
    }
    [Theory]
    [InlineData(1.0)] [InlineData(1.5)] [InlineData(2.0)]
    public void PhysicalPixelsConvertConsistentlyAtDpi(double scale)
    {
        var p = CoordinateTransform.ToDip(new PointerSample(240 * scale, 120 * scale, 12), scale, scale);
        Assert.Equal(240, p.X); Assert.Equal(120, p.Y);
    }
}
