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
    public static IReadOnlyDictionary<string, bool> Run(Window reference, Window overlay)
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
        if(overlay.FindName("AuxiliaryModels") is ModelSelector selector)
        {
            var foreground=GetForegroundWindow(); selector.OpenHoverMenu(ModelSelectorPart.Model); overlay.UpdateLayout();
            var choices=Descendants(selector).OfType<Button>().Where(b => b.Tag is ModelChoice).ToArray();
            results["ModelChoicesUseProtectedHwnd"]=choices.Length>0 && choices.All(b => ReferenceEquals(PresentationSource.FromVisual(b),HwndSource.FromHwnd(hwnd)));
            results["ModelChoicesDoNotTakeFocus"]=choices.All(b => !b.Focusable && !b.IsKeyboardFocusWithin) && GetForegroundWindow()==foreground;
            results["NoEditableControlsInProtectedWindow"]=!Descendants(overlay).OfType<TextBox>().Any(b => b.IsVisible);
            results["MarkdownCopyUsesDwell"]=Descendants(overlay).OfType<Button>().Any(b => b.Content is string label && label=="复制" && DwellBehavior.GetCommand(b) is not null && !b.Focusable);
        }
        return results;
    }
    private static IEnumerable<DependencyObject> Descendants(DependencyObject root)
    { for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++) { var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i); yield return child; foreach(var nested in Descendants(child)) yield return nested; } }
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern nint SendMessage(nint hwnd, uint message, nint wParam, nint lParam);
}
