using ExamHelper.Core.Interaction;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Interaction;
public class DwellControllerTests
{
    [Theory]
    [InlineData(1000)] [InlineData(2000)]
    public void TriggersOnceAtThreshold(int delay)
    {
        var clock = new ManualTimeProvider(); var dwell = new DwellController(clock);
        dwell.Enter("target", TimeSpan.FromMilliseconds(delay));
        clock.Advance(TimeSpan.FromMilliseconds(delay - 1)); Assert.Null(dwell.Tick());
        clock.Advance(TimeSpan.FromMilliseconds(1)); Assert.Equal("target", dwell.Tick());
        clock.Advance(TimeSpan.FromSeconds(5)); Assert.Null(dwell.Tick());
        dwell.Enter("target", TimeSpan.FromMilliseconds(delay)); Assert.Null(dwell.Tick());
        dwell.Leave(); dwell.Enter("target", TimeSpan.FromMilliseconds(delay));
        clock.Advance(TimeSpan.FromMilliseconds(delay)); Assert.Equal("target", dwell.Tick());
    }
    [Fact]
    public void LeaveCancels()
    {
        var clock = new ManualTimeProvider(); var dwell = new DwellController(clock);
        dwell.Enter("x", TimeSpan.FromSeconds(1)); dwell.Leave();
        clock.Advance(TimeSpan.FromSeconds(2)); Assert.Null(dwell.Tick());
    }
    [Fact]
    public void LayoutChangeRequiresPointerReentry()
    {
        var clock = new ManualTimeProvider(); var dwell = new DwellController(clock);
        dwell.Enter("old", TimeSpan.FromSeconds(1)); dwell.InvalidateLayout();
        dwell.Enter("new", TimeSpan.FromSeconds(1)); clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Null(dwell.Tick());
        dwell.Leave(); dwell.Enter("new", TimeSpan.FromSeconds(1)); clock.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal("new", dwell.Tick());
    }
    [Fact]
    public void SuspendCancelsAndNeedsReentry()
    {
        var clock = new ManualTimeProvider(); var dwell = new DwellController(clock);
        dwell.Enter("x", TimeSpan.FromSeconds(1)); dwell.Suspend();
        clock.Advance(TimeSpan.FromSeconds(2)); Assert.Null(dwell.Tick());
        dwell.Enter("x", TimeSpan.FromSeconds(1)); Assert.Null(dwell.Tick());
    }
}
