using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class InputStateRestorerTests
{
    [Theory]
    [InlineData(false, true)] [InlineData(true, false)]
    public void SynchronousImeSetterFailureIsReported(bool conversion, bool open)
    {
        var api = new FakeRestoreApi { ConversionWorks = conversion, OpenWorks = open };
        Assert.False(new InputStateRestorer(api).Restore(12, new(0x804, 1, 2, true)));
        Assert.True(api.Released);
    }
    [Fact]
    public void MissingContextCannotRestoreSavedImeState()
    {
        var api = new FakeRestoreApi { Context = 0 };
        Assert.False(new InputStateRestorer(api).Restore(12, new(0x804, 1, 2, true)));
    }
    [Fact]
    public void MissingLegacyImeContextIsOkayWhenNoLegacyStateWasSaved()
    {
        var api = new FakeRestoreApi { Context = 0 };
        Assert.True(new InputStateRestorer(api).Restore(12, new(0x409, null, null, null)));
    }
    [Fact]
    public void LayoutRestoreFailureIsReported()
    {
        Assert.False(new InputStateRestorer(new FakeRestoreApi { LayoutWorks = false }).Restore(12, new(0x804, null, null, null)));
    }
    private sealed class FakeRestoreApi : IInputRestoreApi
    {
        public bool ConversionWorks = true, OpenWorks = true, LayoutWorks = true, Released;
        public nint Context = 34;
        public bool RestoreLayout(nint hwnd, nint layout) => LayoutWorks;
        public nint GetContext(nint hwnd) => Context;
        public bool SetConversion(nint context, uint conversion, uint sentence) => ConversionWorks;
        public bool SetOpen(nint context, bool open) => OpenWorks;
        public void ReleaseContext(nint hwnd, nint context) => Released = true;
    }
}
