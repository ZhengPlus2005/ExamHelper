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

    [Fact]
    public async Task PhysicalResizeIsNotScaledAgainWhenMovingBetweenMonitorDpis()
    {
        await StaTest.RunAsync(() =>
        {
            var previous = SetThreadDpiAwarenessContext(-4);
            Window? window = null;
            try
            {
                window = new Window { Width = 400,Height = 300,MinWidth = 0,MinHeight = 0,WindowStyle = WindowStyle.None,ShowActivated = false };
                var hwnd = new WindowInteropHelper(window).EnsureHandle();
                foreach(var display in new ExamHelper.Windows.Capture.GdiScreenCaptureBackend().GetDisplays())
                {
                    var expected = new ExamHelper.Core.Settings.PhysicalWindowPlacement(display.X + 40,display.Y + 40,
                        Math.Min(1200,display.Width - 80),Math.Min(1000,display.Height - 80));
                    MonitorGeometry.SetBounds(hwnd,expected); StaTest.Pump();
                    Assert.Equal(expected,MonitorGeometry.WindowBounds(hwnd));
                }
            }
            finally { window?.Close(); if(previous != 0) SetThreadDpiAwarenessContext(previous); }
        });
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);
}
