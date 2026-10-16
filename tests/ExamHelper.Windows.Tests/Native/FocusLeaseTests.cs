using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class FocusLeaseTests
{
    [Fact]
    public void ClosedTargetDoesNotReceiveInput()
    {
        var api = new FakeFocusApi(); var lease = new FocusLease(api);
        Assert.True(lease.Begin(20).Success); api.TargetValid = false;
        Assert.False(lease.End().Success); Assert.Equal((nint)20, api.Foreground);
        Assert.Equal(0, api.Restores);
    }
    [Fact]
    public void LeaveEditorRestoresOnce()
    {
        var api = new FakeFocusApi(); var lease = new FocusLease(api);
        Assert.True(lease.Begin(20).Success); Assert.True(lease.Begin(20).Success);
        Assert.Equal((nint)20, api.Foreground);
        Assert.True(lease.End().Success); Assert.Equal((nint)10, api.Foreground);
        Assert.Equal((nint)0x804, api.Restored.Layout);
        lease.End(); Assert.Equal(1, api.Restores);
    }
    [Fact]
    public void FailedActivationDoesNotEnterEnglish()
    {
        var api = new FakeFocusApi { CanFocus = false }; var lease = new FocusLease(api);
        Assert.False(lease.Begin(20).Success); Assert.Equal(0, api.EnglishSwitches);
    }
    private sealed class FakeFocusApi : IFocusApi
    {
        public nint Foreground = 10; public bool TargetValid = true, CanFocus = true;
        public int Restores, EnglishSwitches; public InputState Restored;
        public nint GetForeground() => Foreground;
        public bool IsWindow(nint hwnd) => hwnd != 10 || TargetValid;
        public InputState ReadInput(nint hwnd) => new(0x804, null, null, null);
        public bool Focus(nint hwnd) { if (!CanFocus) return false; Foreground = hwnd; return true; }
        public bool EnterEnglish(nint hwnd) { EnglishSwitches++; return true; }
        public bool RestoreInput(nint hwnd, InputState state) { if (hwnd == 10) { Restores++; Restored = state; } return true; }
    }
}
