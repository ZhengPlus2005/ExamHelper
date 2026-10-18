using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ExamHelper.Windows.Images;
namespace ExamHelper.App.Images;
public partial class ImageViewerWindow : Window
{
    private readonly ImageViewportTransform _transform=new();
    private readonly byte[] _originalPng;
    private readonly ImageSaveService _save;
    private Point? _drag;
    private bool _manualZoom;
    public double ZoomFactor=>_transform.ZoomFactor;
    public ImageViewerWindow(BitmapSource bitmap):this(bitmap,Encode(bitmap)) { }
    public ImageViewerWindow(BitmapSource bitmap,byte[] originalPng,ImageSaveService? save=null)
    {
        _originalPng=originalPng.ToArray();_save=save ?? new();
        InitializeComponent();Picture.Source=bitmap;Picture.Width=bitmap.PixelWidth;Picture.Height=bitmap.PixelHeight;Picture.RenderTransform=_transform.PixelToViewport;
        Loaded+=(_,_)=>Fit();
    }
    private static byte[] Encode(BitmapSource bitmap)
    { var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var stream=new MemoryStream();encoder.Save(stream);return stream.ToArray(); }
    public void Fit() { _manualZoom=false;_transform.Fit(new Size(Picture.Width,Picture.Height),new Size(Viewport.ActualWidth,Viewport.ActualHeight)); }
    public void ZoomAt(double factor,Point anchor) { _manualZoom=true;_transform.ZoomAt(factor,anchor); }
    public void PanBy(Vector delta) { _manualZoom=true;_transform.Pan(delta); }
    public Task SaveOriginalAsync(string path,CancellationToken ct)=>_save.SaveAsync(path,_originalPng,ct);
    private void FitClick(object sender,RoutedEventArgs e)=>Fit();
    private async void SaveClick(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.SaveFileDialog { Title="保存图片",Filter="PNG 图片|*.png",DefaultExt=".png",AddExtension=true,OverwritePrompt=true,FileName="截图.png" };
        if(dialog.ShowDialog(this)!=true)return;
        SaveButton.IsEnabled=false;
        try { await SaveOriginalAsync(dialog.FileName,CancellationToken.None);SaveStatus.Text="已保存"; }
        catch(Exception error) { SaveStatus.Text=ExamHelper.App.Common.ObservableObject.Explain(error); }
        finally { SaveButton.IsEnabled=true; }
    }
    private void ViewportChanged(object sender,SizeChangedEventArgs e) { if(IsLoaded && !_manualZoom)Fit(); }
    private void Wheel(object sender,MouseWheelEventArgs e) { ZoomAt(e.Delta>0?1.2:1/1.2,e.GetPosition(Viewport));e.Handled=true; }
    private void BeginPan(object sender,MouseButtonEventArgs e) { _drag=e.GetPosition(Viewport);Viewport.CaptureMouse();e.Handled=true; }
    private void EndPan(object sender,MouseButtonEventArgs e) { _drag=null;Viewport.ReleaseMouseCapture(); }
    private void Pan(object sender,MouseEventArgs e) { if(_drag is not { } last)return;var next=e.GetPosition(Viewport);PanBy(next-last);_drag=next; }
    private void KeyDownHandler(object sender,KeyEventArgs e) { if(e.Key==Key.Escape) { Close();e.Handled=true; } }
}
