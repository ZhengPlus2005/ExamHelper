using System.Runtime.InteropServices;
namespace ExamHelper.Windows.Native;
public readonly record struct InputState(nint Layout, uint? Conversion, uint? Sentence, bool? Open);
public static class InputLanguageService
{
    public static InputState Read(nint hwnd)
    {
        var thread = GetWindowThreadProcessId(hwnd, out _);
        var layout = GetKeyboardLayout(thread); var context = ImmGetContext(hwnd);
        if (context == 0) return new(layout, null, null, null);
        try
        {
            var got = ImmGetConversionStatus(context, out var conversion, out var sentence);
            return new(layout, got ? conversion : null, got ? sentence : null, ImmGetOpenStatus(context));
        }
        finally { ImmReleaseContext(hwnd, context); }
    }
    public static bool EnterEnglish(nint hwnd)
    {
        var english = LoadKeyboardLayout("00000409", 0x80); // KLF_NOTELLSHELL: do not reorder user's layout list.
        if (english == 0 || ActivateKeyboardLayout(english, 0) == 0) return false;
        var context = ImmGetContext(hwnd);
        if (context != 0) { try { ImmSetOpenStatus(context, false); } finally { ImmReleaseContext(hwnd, context); } }
        return (Read(hwnd).Layout.ToInt64() & 0xFFFF) == 0x409;
    }
    public static bool Restore(nint hwnd, InputState state)
    {
        var thread = GetWindowThreadProcessId(hwnd, out _);
        if (thread == 0) return false;
        if (GetKeyboardLayout(thread) != state.Layout)
        {
            if (thread == GetCurrentThreadId()) { if (ActivateKeyboardLayout(state.Layout, 0) == 0) return false; }
            else if (!PostMessage(hwnd, 0x0050, 0, state.Layout)) return false;
        }
        var context = ImmGetContext(hwnd);
        if (context != 0)
        {
            try
            {
                if (state.Conversion is uint c && state.Sentence is uint s) ImmSetConversionStatus(context, c, s);
                if (state.Open is bool open) ImmSetOpenStatus(context, open);
            }
            finally { ImmReleaseContext(hwnd, context); }
        }
        return true;
    }
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd, out uint pid);
    [DllImport("user32.dll")] private static extern nint GetKeyboardLayout(uint thread);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern nint LoadKeyboardLayout(string id, uint flags);
    [DllImport("user32.dll")] private static extern nint ActivateKeyboardLayout(nint layout, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();
    [DllImport("imm32.dll")] private static extern nint ImmGetContext(nint hwnd);
    [DllImport("imm32.dll")] private static extern bool ImmReleaseContext(nint hwnd, nint context);
    [DllImport("imm32.dll")] private static extern bool ImmGetConversionStatus(nint context, out uint conversion, out uint sentence);
    [DllImport("imm32.dll")] private static extern bool ImmGetOpenStatus(nint context);
    [DllImport("imm32.dll")] private static extern bool ImmSetOpenStatus(nint context, bool open);
    [DllImport("imm32.dll")] private static extern bool ImmSetConversionStatus(nint context, uint conversion, uint sentence);
}
internal sealed class Win32FocusApi : IFocusApi
{
    public nint GetForeground() => GetForegroundWindow();
    public bool IsWindow(nint hwnd) => IsWindowNative(hwnd);
    public InputState ReadInput(nint hwnd) => InputLanguageService.Read(hwnd);
    public bool Focus(nint hwnd) => SetForegroundWindow(hwnd) && GetForegroundWindow() == hwnd;
    public bool EnterEnglish(nint hwnd) => InputLanguageService.EnterEnglish(hwnd);
    public bool RestoreInput(nint hwnd, InputState state) => InputLanguageService.Restore(hwnd, state);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "IsWindow")] private static extern bool IsWindowNative(nint hwnd);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint hwnd);
}
