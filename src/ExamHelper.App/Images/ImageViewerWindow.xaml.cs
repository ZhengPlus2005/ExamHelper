using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
namespace ExamHelper.App.Images;
public partial class ImageViewerWindow : Window
{
    private Point? _drag;
    public double ZoomFactor => Scale.ScaleX;
    public ImageViewerWindow(BitmapSource bitmap)
    {
        InitializeComponent(); Picture.Source = bitmap; Picture.Width = bitmap.PixelWidth; Picture.Height = bitmap.PixelHeight;
        Loaded += (_,_) => Fit();
    }
    public void Fit()
    {
        var zoom = Math.Min(1,Math.Min(Viewport.ActualWidth/Picture.Width,Viewport.ActualHeight/Picture.Height));
        Scale.ScaleX = Scale.ScaleY = Math.Max(0.01,zoom); Offset.X = (Viewport.ActualWidth-Picture.Width*zoom)/2; Offset.Y = (Viewport.ActualHeight-Picture.Height*zoom)/2;
    }
    public void ZoomAt(double factor,Point anchor)
    {
        var next = Math.Clamp(ZoomFactor*factor,0.01,30); var ratio = next/ZoomFactor;
        Offset.X = anchor.X-(anchor.X-Offset.X)*ratio; Offset.Y = anchor.Y-(anchor.Y-Offset.Y)*ratio;
        Scale.ScaleX = Scale.ScaleY = next;
    }
    private void Wheel(object sender,MouseWheelEventArgs e) { ZoomAt(e.Delta>0 ? 1.2 : 1/1.2,e.GetPosition(Viewport)); e.Handled = true; }
    private void BeginPan(object sender,MouseButtonEventArgs e) { _drag=e.GetPosition(Viewport); Viewport.CaptureMouse(); e.Handled=true; }
    private void EndPan(object sender,MouseButtonEventArgs e) { _drag=null; Viewport.ReleaseMouseCapture(); }
    private void Pan(object sender,MouseEventArgs e) { if(_drag is not { } last) return; var next=e.GetPosition(Viewport); Offset.X+=next.X-last.X; Offset.Y+=next.Y-last.Y; _drag=next; }
    private void KeyDownHandler(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { Close(); e.Handled=true; } }
}
