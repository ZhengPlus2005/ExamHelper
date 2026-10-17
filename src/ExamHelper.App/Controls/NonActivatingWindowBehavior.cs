using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
namespace ExamHelper.App.Controls;

// Auxiliary windows never request text focus. The home/settings windows do not use this behavior.
public sealed class NonActivatingWindowBehavior : IDisposable
{
    private readonly Window _window;
    private readonly HwndSource _source;
    private bool _disposed;
    public NonActivatingWindowBehavior(Window window)
    {
        _window = window;
        window.ShowActivated = false;
        window.Focusable = false;
        KeyboardNavigation.SetTabNavigation(window, KeyboardNavigationMode.None);
        _source = HwndSource.FromHwnd(new WindowInteropHelper(window).EnsureHandle());
        _source.AddHook(Hook);
        window.Closed += OnClosed;
    }
    private static nint Hook(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x0021) { handled = true; return 3; } // MA_NOACTIVATE
        return 0;
    }
    private void OnClosed(object? sender, EventArgs args) => Dispose();
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _window.Closed -= OnClosed;
        if (!_source.IsDisposed) _source.RemoveHook(Hook);
    }
}
