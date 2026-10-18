using System.Windows;
using System.Windows.Media;
namespace ExamHelper.App.Images;
public sealed class ImageViewportTransform
{
    private readonly MatrixTransform _transform=new();
    public Transform PixelToViewport=>_transform;
    public double ZoomFactor=>_transform.Matrix.M11;
    public void Fit(Size pixelSize,Size viewport)
    {
        if(pixelSize.Width<=0 || pixelSize.Height<=0 || viewport.Width<=0 || viewport.Height<=0)return;
        var zoom=Math.Min(1,Math.Min(viewport.Width/pixelSize.Width,viewport.Height/pixelSize.Height));
        _transform.Matrix=new Matrix(zoom,0,0,zoom,(viewport.Width-pixelSize.Width*zoom)/2,(viewport.Height-pixelSize.Height*zoom)/2);
    }
    public void ZoomAt(double factor,Point anchor)
    {
        if(!double.IsFinite(factor) || factor<=0)return;
        var matrix=_transform.Matrix;var next=Math.Clamp(matrix.M11*factor,0.01,30);var ratio=next/matrix.M11;
        _transform.Matrix=new Matrix(next,0,0,next,anchor.X-(anchor.X-matrix.OffsetX)*ratio,anchor.Y-(anchor.Y-matrix.OffsetY)*ratio);
    }
    public void Pan(Vector delta) { var matrix=_transform.Matrix;matrix.Translate(delta.X,delta.Y);_transform.Matrix=matrix; }
}
