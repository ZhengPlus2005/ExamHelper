using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using ExamHelper.Core.Interaction;
namespace ExamHelper.App.Controls;
public sealed class HoverScrollBehavior
{
    private readonly ViewportState _viewport = new();
    public HoverScrollBehavior(ScrollViewer viewer, WindowInteractionSession session)
    {
        viewer.ScrollChanged += (_, e) =>
        {
            if (e.ExtentHeightChange != 0) { if (_viewport.FollowOutput) viewer.ScrollToEnd(); }
            else _viewport.Observe(viewer.VerticalOffset, viewer.ExtentHeight, viewer.ViewportHeight);
        };
        viewer.Loaded += (_, _) =>
        {
            viewer.ApplyTemplate();
            var bar = viewer.Template.FindName("PART_VerticalScrollBar", viewer) as ScrollBar;
            if (bar is null) return;
            bar.Width = 24; bar.ApplyTemplate();
            var track = bar.Template.FindName("PART_Track", bar) as Track;
            if (track?.Thumb is not Thumb thumb) return;
            DwellBehavior.SetCommand(thumb, new DelegateCommand(() =>
            {
                var travel = track.ActualHeight - thumb.ActualHeight;
                if (travel <= 0 || viewer.ScrollableHeight <= 0) return;
                var offset = viewer.VerticalOffset; var ratio = viewer.ScrollableHeight / travel;
                session.Start(InteractionMode.Scrolling, d => viewer.ScrollToVerticalOffset(Math.Clamp(offset + d.DeltaY * ratio, 0, viewer.ScrollableHeight)));
            }));
        };
    }
}
