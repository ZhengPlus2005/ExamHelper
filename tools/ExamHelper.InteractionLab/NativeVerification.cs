using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using ExamHelper.App.Controls;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.InteractionLab;
internal static class NativeVerification
{
    public static IReadOnlyDictionary<string, bool> Run(Window reference, MainWindow overlay)
    {
        var target = new WindowInteropHelper(reference).Handle;
        var hwnd = new WindowInteropHelper(overlay).Handle;
        var results = new Dictionary<string, bool>();
        using var first = new HotkeyRegistry(hwnd);
        using var second = new HotkeyRegistry(target);
        results["DefaultHotkeysRegister"] = first.Register(HotkeyBinding.Defaults).Success;
        results["RealSystemConflictReported"] = !second.Register(HotkeyBinding.Defaults).Success;
        overlay.Hide();
        results["HiddenWindowStillOwnsHotkeys"] = !second.Register(HotkeyBinding.Defaults).Success;
        first.UnregisterAll();
        results["ReleasedHotkeysCanRegisterAgain"] = second.Register(HotkeyBinding.Defaults).Success;
        second.UnregisterAll();
        results["TemporaryEscapeRegisters"] = first.SetManipulationEscape(true);
        results["TemporaryEscapeReleases"] = first.SetManipulationEscape(false);
        overlay.Show();
        var preview = (TextBlock)overlay.FindName("DraftPreview");
        results["DraftPreviewCannotReceiveKeyboardFocus"] = !preview.Focusable && !preview.IsKeyboardFocusWithin;
        results["DraftPreviewHasNoFocusDwellCommand"] = DwellBehavior.GetCommand(preview) is null;
        results["MouseActivationDeclined"] = SendMessage(hwnd, 0x0021, 0, 0) == 3;
        return results;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
