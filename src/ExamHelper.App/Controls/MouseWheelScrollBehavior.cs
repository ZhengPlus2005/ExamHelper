using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public sealed class MouseWheelScrollBehavior
{
    public MouseWheelScrollBehavior(Window window) => window.PreviewMouseWheel += OnWheel;
    private static void OnWheel(object sender,MouseWheelEventArgs args)
    {
        for(DependencyObject? current = args.OriginalSource as DependencyObject;current is not null;current = Parent(current))
        {
            if(current is not ScrollViewer viewer || viewer.ScrollableHeight <= 0) continue;
            var amount = SystemParameters.WheelScrollLines < 0 ? viewer.ViewportHeight : Math.Max(1,SystemParameters.WheelScrollLines) * 16;
            var target = Math.Clamp(viewer.VerticalOffset - args.Delta / 120d * amount,0,viewer.ScrollableHeight);
            if(Math.Abs(target - viewer.VerticalOffset) < 0.01) continue;
            viewer.ScrollToVerticalOffset(target); args.Handled = true; return;
        }
    }
    private static DependencyObject? Parent(DependencyObject value) => value is Visual or System.Windows.Media.Media3D.Visual3D
        ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
}
