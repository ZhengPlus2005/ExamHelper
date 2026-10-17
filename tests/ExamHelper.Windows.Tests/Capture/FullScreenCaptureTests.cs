using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Windows.Capture;
using Xunit;
namespace ExamHelper.Windows.Tests.Capture;
public class FullScreenCaptureTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task RejectsUnsupportedMonitorCounts(int count)
    {
        var backend = new Backend(count);
        var error = await Assert.ThrowsAsync<ServiceFailure>(() => new FullScreenCapture(backend).CaptureAsync(default));
        Assert.Equal(FailureCategory.InvalidPayload, error.Category); Assert.Equal(0, backend.Calls);
    }
    [Fact]
    public async Task CapturesFullPhysicalPixelsWithoutDpiResizing()
    {
        var backend = new Backend(1);
        var result = await new FullScreenCapture(backend).CaptureAsync(default);
        Assert.Equal(2560, result.Width); Assert.Equal(1440, result.Height);
        Assert.Equal(new PhysicalDisplay(-100, 20, 2560, 1440), backend.Requested);
        Assert.Equal(1, backend.Calls);
    }
    [Fact]
    public async Task RejectsMismatchedDimensionsAndCancelledCaptureDoesNotRunBackend()
    {
        var backend = new Backend(1) { WrongSize = true };
        await Assert.ThrowsAsync<ServiceFailure>(() => new FullScreenCapture(backend).CaptureAsync(default));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        backend.Calls = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FullScreenCapture(backend).CaptureAsync(cancel.Token));
        Assert.Equal(0, backend.Calls);
    }
    private sealed class Backend(int count) : IScreenCaptureBackend
    {
        public int Calls; public bool WrongSize; public PhysicalDisplay? Requested;
        public IReadOnlyList<PhysicalDisplay> GetDisplays() => Enumerable.Range(0, count).Select(_ => new PhysicalDisplay(-100,20,2560,1440)).ToArray();
        public CapturedImage Capture(PhysicalDisplay display)
        {
            Calls++; Requested = display; return new([1,2,3], WrongSize ? 1 : display.Width, display.Height);
        }
    }
}