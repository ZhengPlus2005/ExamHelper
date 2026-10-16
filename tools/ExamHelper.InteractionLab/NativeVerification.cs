using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
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
        reference.Activate();
        var before = InputLanguageService.Read(target);
        var lease = new FocusLease();
        var begin = lease.Begin(hwnd);
        results["ActualFocusLeaseBegins"] = begin.Success;
        results["EnglishLayoutSelected"] = (InputLanguageService.Read(hwnd).Layout.ToInt64() & 0xFFFF) == 0x409;
        results["EditorCompositionDisabled"] = !InputMethod.GetIsInputMethodEnabled((TextBox)overlay.FindName("Editor"));
        var end = lease.End();
        results["ActualFocusLeaseRestores"] = end.Success;
        results["OriginalLayoutRestored"] = InputLanguageService.Read(target).Layout == before.Layout;
        return results;
    }
}
