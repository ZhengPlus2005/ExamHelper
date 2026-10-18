using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamHelper.App.Controls;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class DwellOutline04Tests
{
    [Fact] public async Task PerimeterStartsAtTopCenterAndUsesTrueClockwiseArcLengths()
    {
        await StaTest.RunAsync(()=>
        {
            var rect=new Rect(0,0,120,36);Assert.True(RoundedPerimeter.Build(rect,5,0).IsEmpty());var full=RoundedPerimeter.Build(rect,5,1);Assert.Equal(rect,full.Bounds);
            var perimeter=2*(120+36)-8*5+2*Math.PI*5;
            foreach(var progress in new[] { 0.05,0.25,0.5,0.75,1d })
            {
                var path=PathGeometry.CreateFromGeometry(RoundedPerimeter.Build(rect,5,progress));Assert.Equal(new Point(60,0),path.Figures[0].StartPoint);
                var flat=path.GetFlattenedPathGeometry(0.0001,ToleranceType.Absolute);var points=new List<Point> { flat.Figures[0].StartPoint };foreach(var segment in flat.Figures[0].Segments) { if(segment is PolyLineSegment poly)points.AddRange(poly.Points);else if(segment is LineSegment line)points.Add(line.Point); }if(flat.Figures[0].IsClosed)points.Add(points[0]);
                var length=points.Zip(points.Skip(1),(a,b)=>(b-a).Length).Sum();Assert.InRange(length,perimeter*progress-0.01,perimeter*progress+0.01);
                path.GetPointAtFractionLength(1,out var end,out _);var expected=progress switch { 0.25=>new Point(120,18),0.5=>new Point(60,36),0.75=>new Point(0,18),1=>new Point(60,0),_=>end };Assert.InRange((end-expected).Length,0,0.01);
            }
            Assert.Equal(rect,RoundedPerimeter.Build(rect,100,2).Bounds);Assert.True(RoundedPerimeter.Build(rect,5,-1).IsEmpty());
        });
    }
    [Fact] public async Task SharedTemplateDrawsOutsideWithoutClippingAndDoesNotCoverLabel()
    {
        await StaTest.RunAsync(()=>
        {
            var resources=new ResourceDictionary { Source=new Uri("/ExamHelper.App;component/Controls/AuxiliaryDwellButton.xaml",UriKind.Relative) };var button=new Button { Width=120,Height=36,Content="test",Style=(Style)resources["AuxiliaryDwellButtonStyle"],Margin=new Thickness(20) };var window=new Window { Width=240,Height=180,Content=new Grid { Children={button} } };window.Show();window.UpdateLayout();StaTest.Pump();
            try
            {
                var outline=Assert.IsType<DwellOutline>(button.Template.FindName("Outline",button));outline.Progress=1;outline.Stroke=Brushes.Red;window.UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var top=button.TranslatePoint(new Point(button.ActualWidth/2,-1),window);var pixel=new byte[4];bitmap.CopyPixels(new Int32Rect((int)top.X,(int)top.Y,1,1),pixel,4,0);Assert.Equal(new byte[] { 0,0,255,255 },pixel);
                Assert.False(button.Focusable);Assert.True(button.Margin.Left>=outline.StrokeThickness);Assert.Equal(36,button.ActualHeight);
            }
            finally { window.Close();StaTest.Pump(); }
        });
    }
    [Fact] public async Task TimingDependencyOnlyChangesLabelAndOutlineIsPassiveOutsideButton()
    {
        await StaTest.RunAsync(()=>
        {
            var answer=new AnswerView { Text="```cpp\nint x;\n```" };answer.Measure(new Size(400,500));answer.Arrange(new Rect(0,0,400,500));answer.UpdateLayout();var segments=Assert.IsType<StackPanel>(answer.FindName("Segments"));var body=segments.Children[0];
            for(var i=0;i<30;i++)answer.TimingText="已用 "+i+" 秒";Assert.Same(body,segments.Children[0]);answer.TimingText="用时 12.4 秒";Assert.Equal(answer.TimingText,Assert.IsType<TextBlock>(answer.FindName("TimingLabel")).Text);
            var outline=new DwellOutline { Width=120,Height=36,Progress=1 };outline.Measure(new Size(120,36));outline.Arrange(new Rect(0,0,120,36));outline.UpdateLayout();
            Assert.False(outline.IsHitTestVisible);Assert.False(outline.Focusable);Assert.Equal(2,outline.StrokeThickness);
            var bounds=VisualTreeHelper.GetDescendantBounds(outline);Assert.True(bounds.Left<0 && bounds.Top<0 && bounds.Right>120 && bounds.Bottom>36);
        });
    }
}
