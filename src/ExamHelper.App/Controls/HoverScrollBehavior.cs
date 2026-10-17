using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using ExamHelper.Core.Interaction;
namespace ExamHelper.App.Controls;

public sealed class HoverScrollBehavior
{
    private readonly ViewportState _viewport = new();
    private readonly ScrollViewer _viewer;
    private readonly WindowInteractionSession _session;
    private Thumb? _thumb;
    private bool _ownsScroll;

    public HoverScrollBehavior(ScrollViewer viewer, WindowInteractionSession session)
    {
        _viewer = viewer;
        _session = session;
        viewer.Loaded += OnLoaded;
        viewer.Unloaded += OnUnloaded;
        if (viewer.IsLoaded) Attach();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => Attach();
    private void OnUnloaded(object sender, RoutedEventArgs args) => Detach();

    private void Attach()
    {
        if (_thumb is not null) return;
        _viewer.ApplyTemplate();
        if (_viewer.Template.FindName("PART_VerticalScrollBar", _viewer) is not ScrollBar bar) return;
        bar.Width = 24;
        bar.ApplyTemplate();
        if (bar.Template.FindName("PART_Track", bar) is not Track track || track.Thumb is not Thumb thumb) return;

        // Aero2 normally disables the track when the pointer leaves the bar.
        track.SetBinding(UIElement.IsEnabledProperty,
            new Binding(nameof(UIElement.IsEnabled)) { Source = bar, Mode = BindingMode.OneWay });
        _thumb = thumb;
        thumb.MinHeight = 24;
        thumb.Opacity = 0.65;
        _viewer.ScrollChanged += OnScrollChanged;
        _session.ActiveChanged += OnActiveChanged;
        DwellBehavior.SetCommand(thumb, new DelegateCommand(() =>
        {
            if (_session.IsActive) return;
            var travel = track.ActualHeight - thumb.ActualHeight;
            if (travel <= 0 || _viewer.ScrollableHeight <= 0) return;
            var offset = _viewer.VerticalOffset;
            var ratio = _viewer.ScrollableHeight / travel;
            _session.Start(InteractionMode.Scrolling, d =>
                _viewer.ScrollToVerticalOffset(Math.Clamp(offset + d.DeltaY * ratio, 0, _viewer.ScrollableHeight)));
            _ownsScroll = _session.Mode == InteractionMode.Scrolling;
        }));
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs args)
    {
        if (args.ExtentHeightChange != 0)
        {
            if (_viewport.FollowOutput) _viewer.ScrollToEnd();
        }
        else _viewport.Observe(_viewer.VerticalOffset, _viewer.ExtentHeight, _viewer.ViewportHeight);
    }

    private void OnActiveChanged(bool active)
    {
        if (!active) _ownsScroll = false;
        if (_thumb is not null)
            _thumb.Opacity = active && _session.Mode == InteractionMode.Scrolling ? 1 : 0.65;
    }

    private void Detach()
    {
        _viewer.ScrollChanged -= OnScrollChanged;
        _session.ActiveChanged -= OnActiveChanged;
        if (_thumb is not null) _thumb.ClearValue(DwellBehavior.CommandProperty);
        _thumb = null;
        if (_ownsScroll) _session.End();
        _ownsScroll = false;
    }
}
