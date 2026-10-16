using ExamHelper.InteractionLab;
using Xunit;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ExamHelper.App.Controls;
namespace ExamHelper.Windows.Tests.Native;
public class LabTemplateTests
{
    [Fact]
    public async Task LabTemplateLoadsAndMeasuresWithoutReadOnlyBindingErrors()
    {
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            MainWindow? window = null;
            try
            {
                window = new MainWindow(registerHotkeys: false);
                window.Show(); window.UpdateLayout(); Assert.True(window.IsVisible);
                var frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => frame.Continue = false);
                Dispatcher.PushFrame(frame);
                var scroll = (ScrollViewer)window.FindName("AnswerScroll");
                var bar = Assert.IsType<ScrollBar>(scroll.Template.FindName("PART_VerticalScrollBar", scroll));
                var track = Assert.IsType<Track>(bar.Template.FindName("PART_Track", bar));
                Assert.NotNull(DwellBehavior.GetCommand(track.Thumb));
                Assert.True(track.Thumb.IsEnabled, $"thumb={track.Thumb.IsEnabled}; track={track.IsEnabled}; bar={bar.IsEnabled}; min={bar.Minimum}; max={bar.Maximum}; viewport={bar.ViewportSize}; scroll={scroll.IsEnabled}; extent={scroll.ExtentHeight}; visible={scroll.ViewportHeight}");
                Assert.True(scroll.ScrollableHeight > 0);
                result.SetResult();
            }
            catch (Exception ex) { result.SetException(ex); }
            finally { window?.Close(); System.Windows.Threading.Dispatcher.CurrentDispatcher.InvokeShutdown(); }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        await result.Task.WaitAsync(TimeSpan.FromSeconds(10)); thread.Join(1000);
    }
}
