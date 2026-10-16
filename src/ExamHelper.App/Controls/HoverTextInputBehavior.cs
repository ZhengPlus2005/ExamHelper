using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using ExamHelper.Windows.Native;
namespace ExamHelper.App.Controls;
public sealed class HoverTextInputBehavior : IDisposable
{
    private readonly Window _window;
    private readonly TextBox _editor;
    private readonly FocusLease _lease = new();
    private readonly HwndSource _source;
    private readonly HwndSourceHook _hook;
    public event Action<string>? StatusChanged;
    public HoverTextInputBehavior(Window window, TextBox editor)
    {
        _window = window; _editor = editor;
        var hwnd = new WindowInteropHelper(window).EnsureHandle();
        _source = HwndSource.FromHwnd(hwnd); _hook = Hook; _source.AddHook(_hook);
        InputMethod.SetIsInputMethodEnabled(editor, false);
        DwellBehavior.SetCommand(editor, new DelegateCommand(() =>
        {
            var result = _lease.Begin(hwnd);
            if (result.Success) { window.Activate(); editor.Focus(); Keyboard.Focus(editor); }
            else StatusChanged?.Invoke(result.Error!);
        }));
        window.IsVisibleChanged += VisibilityChanged; window.Closed += Closed;
    }
    private nint Hook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x0021 && !_lease.IsActive) { handled = true; return 3; } // MA_NOACTIVATE
        return 0;
    }
    public void CancelInput()
    {
        Keyboard.ClearFocus(); FocusManager.SetFocusedElement(_window, null);
        var result = _lease.End(); if (!result.Success) StatusChanged?.Invoke(result.Error!);
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (!_window.IsVisible) CancelInput(); }
    private void Closed(object? sender, EventArgs e) => Dispose();
    public void Dispose()
    {
        CancelInput(); _source.RemoveHook(_hook);
        _window.IsVisibleChanged -= VisibilityChanged; _window.Closed -= Closed;
    }
}
