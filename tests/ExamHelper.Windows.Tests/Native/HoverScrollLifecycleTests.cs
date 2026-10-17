using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using ExamHelper.App.Controls;
using ExamHelper.Core.Interaction;
using Xunit;

namespace ExamHelper.Windows.Tests.Native;

public class HoverScrollLifecycleTests
{
    [Fact]
    public async Task UnloadingScrollViewerRemovesHoverCommandAndEndsOwnedScroll()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var viewer = new ScrollViewer
            {
                Content = new TextBlock { Text = string.Join("\n", Enumerable.Repeat("scroll content", 100)) },
                VerticalScrollBarVisibility = ScrollBarVisibility.Visible
            };
            var host = new Grid(); host.Children.Add(viewer);
            var window = new Window { Content = host, Width = 400, Height = 300, ShowActivated = false };
            using var session = new WindowInteractionSession(window);
            _ = new HoverScrollBehavior(viewer, session);
            try
            {
                window.Show(); Pump(); window.UpdateLayout();
                var thumb = GetThumb(viewer);
                var command = DwellBehavior.GetCommand(thumb);
                Assert.NotNull(command);
                command.Execute(null);
                Assert.Equal(InteractionMode.Scrolling, session.Mode);

                host.Children.Remove(viewer); Pump();
                Assert.Null(DwellBehavior.GetCommand(thumb));
                Assert.False(session.IsActive);
                var opacity = thumb.Opacity;
                session.Start(InteractionMode.Moving, _ => { });
                session.End();
                Assert.Equal(opacity, thumb.Opacity);

                host.Children.Add(viewer); Pump(); window.UpdateLayout();
                var reloaded = GetThumb(viewer);
                Assert.NotNull(DwellBehavior.GetCommand(reloaded));
                DwellBehavior.GetCommand(reloaded)!.Execute(null);
                Assert.Equal(InteractionMode.Scrolling, session.Mode);
                session.End();
                completion.SetResult();
            }
            catch (Exception error) { completion.SetException(error); }
            finally { window.Close(); Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(15)); thread.Join(1000);
    }

    private static Thumb GetThumb(ScrollViewer viewer)
    {
        viewer.ApplyTemplate();
        var bar = Assert.IsType<ScrollBar>(viewer.Template.FindName("PART_VerticalScrollBar", viewer));
        bar.ApplyTemplate();
        var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
        return track.Thumb;
    }

    private static void Pump()
    {
        var frame = new DispatcherFrame();
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
        Dispatcher.PushFrame(frame);
    }
}
