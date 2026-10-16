using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ExamHelper.Core.Interaction;
namespace ExamHelper.App.Controls;

public static class DwellBehavior
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached("Command", typeof(ICommand), typeof(DwellBehavior), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty DelayProperty = DependencyProperty.RegisterAttached("Delay", typeof(double), typeof(DwellBehavior), new PropertyMetadata(1d));
    private static readonly DependencyPropertyKey ProgressKey = DependencyProperty.RegisterAttachedReadOnly("Progress", typeof(double), typeof(DwellBehavior), new PropertyMetadata(0d));
    public static readonly DependencyProperty ProgressProperty = ProgressKey.DependencyProperty;
    private static readonly ConditionalWeakTable<FrameworkElement, State> States = new();
    private static readonly ConditionalWeakTable<Window, Scope> Scopes = new();
    public static void SetCommand(DependencyObject o, ICommand value) => o.SetValue(CommandProperty, value);
    public static ICommand? GetCommand(DependencyObject o) => (ICommand?)o.GetValue(CommandProperty);
    public static void SetDelay(DependencyObject o, double value) => o.SetValue(DelayProperty, value);
    public static double GetDelay(DependencyObject o) => (double)o.GetValue(DelayProperty);
    public static double GetProgress(DependencyObject o) => (double)o.GetValue(ProgressProperty);
    public static void SetSuspended(Window window, bool value)
    {
        var scope = Scopes.GetOrCreateValue(window); scope.Suspended = value;
        foreach (var state in scope.States.ToArray()) state.Block();
    }
    public static void InvalidateWindow(Window window)
    {
        if (Scopes.TryGetValue(window, out var scope)) foreach (var state in scope.States.ToArray()) state.Block();
    }
    private static void Changed(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element && !States.TryGetValue(element, out _)) States.Add(element, new State(element));
    }
    private sealed class Scope { public bool Suspended; public List<State> States { get; } = []; }
    private sealed class State
    {
        private readonly FrameworkElement _element;
        private readonly DwellController _dwell = new(TimeProvider.System);
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
        private Scope? _scope;
        private Point? _blockedAt;
        private Rect _bounds;
        public State(FrameworkElement element)
        {
            _element = element;
            element.Loaded += (_, _) => Attach(); element.Unloaded += (_, _) => Detach();
            element.MouseEnter += (_, _) => Enter(); element.MouseLeave += (_, _) => Leave();
            element.IsEnabledChanged += (_, _) => Block(); element.IsVisibleChanged += (_, _) => Block();
            element.LayoutUpdated += (_, _) =>
            {
                if (!element.IsLoaded || !element.IsVisible) return;
                var bounds = new Rect(element.PointToScreen(new Point()), element.RenderSize);
                if (_bounds != bounds) { _bounds = bounds; if (element.IsMouseOver) Block(); }
            };
            _timer.Tick += (_, _) => Tick(); if (element.IsLoaded) Attach();
        }
        private void Attach()
        {
            var window = Window.GetWindow(_element); if (window is null) return;
            _scope = Scopes.GetOrCreateValue(window); if (!_scope.States.Contains(this)) _scope.States.Add(this);
            if (_element.IsMouseOver) Block(); else Leave();
        }
        private void Detach() { _scope?.States.Remove(this); _scope = null; Block(); }
        private Point? ScreenPoint() => PresentationSource.FromVisual(_element) is null ? null : _element.PointToScreen(Mouse.GetPosition(_element));
        public void Block() { _dwell.Suspend(); _timer.Stop(); _element.SetValue(ProgressKey, 0d); _blockedAt = ScreenPoint(); }
        private void Leave()
        {
            _timer.Stop(); _element.SetValue(ProgressKey, 0d);
            // Layout can synthesize MouseLeave/MouseEnter without the cursor moving.
            if (_blockedAt is Point blocked && ScreenPoint() is Point current && (blocked - current).Length <= 1) return;
            _dwell.Leave(); _blockedAt = null;
        }
        private void Enter()
        {
            if (_blockedAt is not null || _scope?.Suspended == true || !_element.IsEnabled || !_element.IsVisible) return;
            _dwell.Enter("command", TimeSpan.FromSeconds(Math.Max(0.05, GetDelay(_element)))); _timer.Start();
        }
        private void Tick()
        {
            var command = GetCommand(_element);
            if (_scope?.Suspended == true || !_element.IsMouseOver || !_element.IsEnabled || command?.CanExecute(null) != true) { Block(); return; }
            _element.SetValue(ProgressKey, _dwell.Progress);
            if (_dwell.Tick() is null) return;
            _timer.Stop(); _element.SetValue(ProgressKey, 0d); command.Execute(null);
        }
    }
}
