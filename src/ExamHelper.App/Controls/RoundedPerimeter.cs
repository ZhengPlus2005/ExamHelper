using System.Windows;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public static class RoundedPerimeter
{
    public static Geometry Build(Rect bounds,double cornerRadius,double progress)
    {
        if(bounds.IsEmpty || bounds.Width<=0 || bounds.Height<=0 || !double.IsFinite(progress) || progress<=0)return Geometry.Empty;
        progress=Math.Min(1,progress);var r=Math.Clamp(double.IsFinite(cornerRadius)?cornerRadius:0,0,Math.Min(bounds.Width,bounds.Height)/2);
        var remaining=(2*(bounds.Width+bounds.Height)-8*r+2*Math.PI*r)*progress;
        var start=new Point(bounds.Left+bounds.Width/2,bounds.Top);var figure=new PathFigure { StartPoint=start,IsClosed=progress==1 };var current=start;
        void Line(Point end)
        {
            var length=(end-current).Length;if(length<=0 || remaining<=0)return;var used=Math.Min(length,remaining);var next=current+(end-current)*(used/length);figure.Segments.Add(new LineSegment(next,true));current=next;remaining-=used;
        }
        void Arc(Point center,double angle)
        {
            if(r==0 || remaining<=0)return;var used=Math.Min(Math.PI*r/2,remaining);var endAngle=angle+used/r;var next=new Point(center.X+r*Math.Cos(endAngle),center.Y+r*Math.Sin(endAngle));
            figure.Segments.Add(new ArcSegment(next,new Size(r,r),0,false,SweepDirection.Clockwise,true));current=next;remaining-=used;
        }
        Line(new Point(bounds.Right-r,bounds.Top));Arc(new Point(bounds.Right-r,bounds.Top+r),-Math.PI/2);
        Line(new Point(bounds.Right,bounds.Bottom-r));Arc(new Point(bounds.Right-r,bounds.Bottom-r),0);
        Line(new Point(bounds.Left+r,bounds.Bottom));Arc(new Point(bounds.Left+r,bounds.Bottom-r),Math.PI/2);
        Line(new Point(bounds.Left,bounds.Top+r));Arc(new Point(bounds.Left+r,bounds.Top+r),Math.PI);
        Line(start);
        return new PathGeometry([figure]);
    }
}
