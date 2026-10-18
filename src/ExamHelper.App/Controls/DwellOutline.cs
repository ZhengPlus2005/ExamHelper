using System.Windows;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public sealed class DwellOutline:FrameworkElement
{
    private static readonly FrameworkPropertyMetadataOptions Render=FrameworkPropertyMetadataOptions.AffectsRender;
    public static readonly DependencyProperty ProgressProperty=DependencyProperty.Register(nameof(Progress),typeof(double),typeof(DwellOutline),new FrameworkPropertyMetadata(0d,Render));
    public static readonly DependencyProperty StrokeProperty=DependencyProperty.Register(nameof(Stroke),typeof(Brush),typeof(DwellOutline),new FrameworkPropertyMetadata(Brushes.SeaGreen,Render));
    public static readonly DependencyProperty StrokeThicknessProperty=DependencyProperty.Register(nameof(StrokeThickness),typeof(double),typeof(DwellOutline),new FrameworkPropertyMetadata(2d,Render));
    public static readonly DependencyProperty CornerRadiusProperty=DependencyProperty.Register(nameof(CornerRadius),typeof(double),typeof(DwellOutline),new FrameworkPropertyMetadata(5d,Render));
    public double Progress { get=>(double)GetValue(ProgressProperty);set=>SetValue(ProgressProperty,value); }
    public Brush Stroke { get=>(Brush)GetValue(StrokeProperty);set=>SetValue(StrokeProperty,value); }
    public double StrokeThickness { get=>(double)GetValue(StrokeThicknessProperty);set=>SetValue(StrokeThicknessProperty,value); }
    public double CornerRadius { get=>(double)GetValue(CornerRadiusProperty);set=>SetValue(CornerRadiusProperty,value); }
    public DwellOutline() { Focusable=false;IsHitTestVisible=false;ClipToBounds=false; }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);if(ActualWidth<=0 || ActualHeight<=0 || !double.IsFinite(StrokeThickness) || StrokeThickness<=0)return;
        var half=StrokeThickness/2;var bounds=new Rect(-half,-half,ActualWidth+StrokeThickness,ActualHeight+StrokeThickness);
        context.DrawGeometry(null,new Pen(Stroke,StrokeThickness),RoundedPerimeter.Build(bounds,CornerRadius+half,Progress));
    }
}
