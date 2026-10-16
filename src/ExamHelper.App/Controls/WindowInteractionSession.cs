using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ExamHelper.Core.Interaction;
using ExamHelper.Windows.Native;
namespace ExamHelper.App.Controls;

// One session per protected window: all manipulations share its timer and lock.
public sealed class WindowInteractionSession : IDisposable
{
    private readonly Window _window;
    private readonly ManipulationController _controller = new(TimeProvider.System);
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private Action<ManipulationUpdate>? _apply;
    public event Action<bool>? ActiveChanged;
    public bool IsActive => _controller.Mode != InteractionMode.Idle;
    public InteractionMode Mode => _controller.Mode;
    public WindowInteractionSession(Window window)
    {
        _window = window; _timer.Tick += Tick;
        window.Closed += Closed; window.IsVisibleChanged += VisibilityChanged;
    }
    private PointerSample Read()
    {
        var dpi = VisualTreeHelper.GetDpi(_window); return CoordinateTransform.ToDip(PointerSampler.Read(), dpi.DpiScaleX, dpi.DpiScaleY);
    }
    public void Start(InteractionMode mode, Action<ManipulationUpdate> apply)
    {
        if (IsActive || !_window.IsVisible) return;
        _controller.Begin(mode, Read()); _apply = apply;
        DwellBehavior.SetSuspended(_window, true); _timer.Start(); ActiveChanged?.Invoke(true);
    }
    public void End()
    {
        if (!IsActive && _apply is null) return;
        _timer.Stop(); _controller.End(); _apply = null;
        DwellBehavior.SetSuspended(_window, false); ActiveChanged?.Invoke(false);
    }
    private void Tick(object? sender, EventArgs args)
    {
        try { var update = _controller.Update(Read()); _apply?.Invoke(update); if (update.IsFinished) End(); }
        catch { End(); throw; }
    }
    private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { if (!_window.IsVisible) End(); }
    private void Closed(object? sender, EventArgs e) => Dispose();
    public void Dispose() { End(); _window.Closed -= Closed; _window.IsVisibleChanged -= VisibilityChanged; _timer.Tick -= Tick; }
}
