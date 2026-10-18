using System.Windows;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public sealed class HoverDeleteRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(HoverDeleteRing),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty,value); }
    public Color ProgressColor => Colors.Red;
    public HoverDeleteRing() { Focusable=false; IsHitTestVisible=false; }
    protected override void OnRender(DrawingContext dc)
    {
        var progress=Math.Clamp(Progress,0,1); if(progress<=0) return;
        var center=new Point(ActualWidth/2,ActualHeight/2); var radius=Math.Max(0,Math.Min(ActualWidth,ActualHeight)/2-2); var pen=new Pen(Brushes.Red,3);
        if(progress>=0.999) { dc.DrawEllipse(null,pen,center,radius,radius); return; }
        var angle=progress*Math.PI*2; var end=new Point(center.X+radius*Math.Sin(angle),center.Y-radius*Math.Cos(angle));
        var geometry=new StreamGeometry(); using(var path=geometry.Open()) { path.BeginFigure(new(center.X,center.Y-radius),false,false); path.ArcTo(end,new Size(radius,radius),0,progress>0.5,SweepDirection.Clockwise,true,false); }
        geometry.Freeze(); dc.DrawGeometry(null,pen,geometry);
    }
}
