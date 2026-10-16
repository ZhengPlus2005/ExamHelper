using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class WindowProtectionTests
{
    [Theory]
    [InlineData(false, true, 0x11u, 5)]
    [InlineData(true, false, 0x11u, 6)]
    [InlineData(true, true, 0x01u, 0)]
    public void RejectsNativeFailureAndAffinityMismatch(bool set, bool read, uint affinity, int error)
    {
        var result = new WindowProtection(new FakeApi(set, read, affinity, error)).ApplyAndVerify(123);
        Assert.False(result.Success);
        Assert.Equal(error, result.ErrorCode);
    }
    [Fact]
    public void AcceptsVerifiedCaptureExclusion()
    {
        var api = new FakeApi(true, true, 0x11, 0);
        var result = new WindowProtection(api).ApplyAndVerify(123);
        Assert.True(result.Success);
        Assert.Equal(0x11u, result.EffectiveAffinity);
        Assert.Equal(0x11u, api.Requested);
    }
    private sealed class FakeApi(bool set, bool read, uint affinity, int error) : IWindowProtectionApi
    {
        public uint Requested { get; private set; }
        public bool Set(nint hwnd, uint value, out int code) { Requested = value; code = error; return set; }
        public bool Get(nint hwnd, out uint value, out int code) { value = affinity; code = error; return read; }
    }
}
