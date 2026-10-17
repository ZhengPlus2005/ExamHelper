using ExamHelper.App.Controls;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using ExamHelper.InteractionLab;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Interop;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class VoiceOnlyDraftTests
{
    [Fact]
    public async Task DraftPreviewCannotReceiveKeyboardFocusButAcceptsSpeechText()
    {
        await RunStaAsync(() =>
        {
            var window = new MainWindow(registerHotkeys: false);
            try
            {
                window.Show(); window.UpdateLayout();
                var preview = Assert.IsType<TextBlock>(window.FindName("DraftPreview"));
                Assert.False(preview.Focusable); Assert.False(preview.Focus());
                Assert.Null(DwellBehavior.GetCommand(preview));
                Assert.Null(window.FindName("CancelButton"));
                var append = Assert.IsType<Button>(window.FindName("InsertChinese"));
                append.Command.Execute(null); append.Command.Execute(null);
                Assert.Equal("模拟识别：请用 C++ 回答。\n模拟识别：请用 C++ 回答。\n", preview.Text);
                Assert.IsType<Button>(window.FindName("ClearButton")).Command.Execute(null);
                Assert.Equal("", preview.Text);
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public async Task ClickingOverlayDoesNotRequestWindowActivation()
    {
        await RunStaAsync(() =>
        {
            var window = new MainWindow(registerHotkeys: false);
            try
            {
                window.Show(); window.UpdateLayout();
                Assert.Equal((nint)3, SendMessage(new WindowInteropHelper(window).Handle, 0x0021, 0, 0));
                Assert.False(window.ShowActivated);
            }
            finally { window.Close(); }
        });
    }
    [Fact]
    public void RemovingCancelDoesNotShiftVisibilityOrExitShortcuts()
    {
        Assert.DoesNotContain(HotkeyBinding.Defaults, b => b.Modifiers == 3 && b.VirtualKey == 0x74);
        Assert.Contains(HotkeyBinding.Defaults, b => b.Command == AppCommand.ToggleVisibility && b.Modifiers == 3 && b.VirtualKey == 0x78);
        Assert.Contains(HotkeyBinding.Defaults, b => b.Command == AppCommand.Exit && b.Modifiers == 3 && b.VirtualKey == 0x7B);
    }
    private static async Task RunStaAsync(Action action)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { action(); completion.SetResult(); }
            catch (Exception error) { completion.SetException(error); }
            finally { System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15)); thread.Join(1000);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
