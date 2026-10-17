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
    [Fact]
    public async Task MultipleDisplaysFollowOwnerWindowAndKeepPhysicalDimensions()
    {
        var backend = new WindowBackend(); nint owner = 2;
        var capture = new FullScreenCapture(backend,() => owner);
        var result = await capture.CaptureAsync(default);
        Assert.Equal(new PhysicalDisplay(-2560,-200,2560,1440),backend.Requested);
        Assert.Equal(2560,result.Width); Assert.Equal(1440,result.Height);
        owner = 1; result = await capture.CaptureAsync(default);
        Assert.Equal(new PhysicalDisplay(0,0,1920,1080),backend.Requested);
        Assert.Equal(1920,result.Width); Assert.Equal(2,backend.OwnerReads);
    }
    private sealed class WindowBackend : IScreenCaptureBackend
    {
        public PhysicalDisplay? Requested; public int OwnerReads;
        public IReadOnlyList<PhysicalDisplay> GetDisplays() => [new(0,0,1920,1080),new(-2560,-200,2560,1440)];
        public PhysicalDisplay GetWindowDisplay(nint owner) { OwnerReads++; return GetDisplays()[owner == 2 ? 1 : 0]; }
        public CapturedImage Capture(PhysicalDisplay display) { Requested = display; return new([1],display.Width,display.Height); }
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