using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public static class ChatWheelRouting
{
    public static void Attach(ScrollViewer code)
    {
        code.PreviewMouseWheel+=(_,e)=>
        {
            DependencyObject? node=VisualTreeHelper.GetParent(code);
            while(node is not null && node is not ScrollViewer)node=VisualTreeHelper.GetParent(node);
            if(node is not ScrollViewer outer)return;
            ForwardVertical(outer,e.Delta);e.Handled=true;
        };
    }
    public static void ForwardVertical(ScrollViewer outer,int delta)
    {
        var distance=SystemParameters.WheelScrollLines<0?outer.ViewportHeight:SystemParameters.WheelScrollLines*16;
        outer.ScrollToVerticalOffset(outer.VerticalOffset-delta/120d*distance);
    }
}
