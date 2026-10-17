using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using ExamHelper.App.Controls;
using ExamHelper.Windows.Native;
using ExamHelper.Windows.Tests.App;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class MonitorGeometryTests
{
    [Fact]
    public async Task HiddenWindowKeepsItsMonitorAndPhysicalMovementKeepsCollapsedMinimum()
    {
        await StaTest.RunAsync(() =>
        {
            var move = new Border { Height = 20 }; var resize = new Border { Height = 20 };
            var panel = new StackPanel(); panel.Children.Add(move); panel.Children.Add(resize);
            var window = new Window { Width = 500,Height = 240,MinWidth = 300,MinHeight = 180,Content = panel,ShowActivated = false };
            using var session = new WindowInteractionSession(window,physicalPointer: true);
            _ = new WindowManipulationBehavior(window,move,resize,session);
            try
            {
                window.Show(); StaTest.Pump(); var hwnd = new WindowInteropHelper(window).Handle;
                var before = MonitorGeometry.ForWindow(hwnd); window.Hide();
                Assert.Equal(before,MonitorGeometry.ForWindow(hwnd)); window.Show(); StaTest.Pump();
                DwellBehavior.GetCommand(move)!.Execute(null); session.End(); window.UpdateLayout();
                Assert.Equal(180,window.MinHeight);
                Assert.True(MonitorGeometry.WindowBounds(hwnd).Height < 400);
            }
            finally { window.Close(); }
        });
    }
}
