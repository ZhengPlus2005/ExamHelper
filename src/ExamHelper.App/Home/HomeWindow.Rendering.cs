using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExamHelper.App.Images;
using ExamHelper.Core.Drafts;
namespace ExamHelper.App.Home;
public partial class HomeWindow
{
    private long _renderedRevision=-1;
    private Guid? _renderedSession;
    private Guid[] _draftImageIds=[];
    private readonly Dictionary<Guid,BitmapSource> _thumbnails=[];
    private void Render(bool force)
    {
        var snapshot=_viewModel.Snapshot; var newSession=_renderedSession!=snapshot?.Id;
        SyncInput(newSession);
        if(!force && _renderedRevision==_viewModel.ViewRevision) return;
        _renderedRevision=_viewModel.ViewRevision; _renderedSession=snapshot?.Id;
        if(newSession) _thumbnails.Clear();
        _syncSelection=true;
        try
        {
            var selected=_viewModel.History.FirstOrDefault(s => s.Id==snapshot?.Id);
            if(!ReferenceEquals(HistoryList.SelectedItem,selected)) HistoryList.SelectedItem=selected;
        }
        finally { _syncSelection=false; }
        HomeModels.Configure(_viewModel.SettingsSnapshot,snapshot?.Selection,false,_viewModel.Verification);
        var offset=HistoryScroll.VerticalOffset; var atBottom=newSession || HistoryScroll.ScrollableHeight-offset<20;
        HistoryMessages.Children.Clear();
        foreach(var message in _viewModel.Messages)
        {
            var panel=new StackPanel { Margin=new Thickness(0,0,0,20),Tag=message.Message.Id };
            panel.Children.Add(new TextBlock { Text=message.Header,FontWeight=FontWeights.SemiBold,Foreground=new SolidColorBrush(Color.FromRgb(67,89,116)),Margin=new Thickness(0,0,0,7) });
            if(message.Images.Length>0)
            {
                var pictures=new WrapPanel(); foreach(var image in message.Images) pictures.Children.Add(Thumbnail(image,false)); panel.Children.Add(pictures);
            }
            panel.Children.Add(message.Message.Role == ExamHelper.Core.Sessions.MessageRole.Assistant ? new ExamHelper.App.Controls.MarkdownView { Text=message.Text } : new TextBlock { Text=message.Text,TextWrapping=TextWrapping.Wrap,FontSize=14,Foreground=new SolidColorBrush(Color.FromRgb(25,35,49)) });
            HistoryMessages.Children.Add(panel);
        }
        var ids=_viewModel.DraftImages.Select(i => i.Id).ToArray();
        if(newSession || !_draftImageIds.SequenceEqual(ids))
        {
            _draftImageIds=ids; HomeDraftPictures.Children.Clear(); foreach(var image in _viewModel.DraftImages) HomeDraftPictures.Children.Add(Thumbnail(image,true));
        }
        var session=_renderedSession;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,() => { if(_renderedSession!=session) return; if(atBottom) HistoryScroll.ScrollToEnd(); else HistoryScroll.ScrollToVerticalOffset(offset); });
    }
    private void SyncInput(bool newSession)
    {
        HomeDraftInput.IsEnabled=_viewModel.SelectedConversationId is not null;
        if(_viewModel.SelectedConversationId is not { } id) { _syncInput=true; try { HomeDraftInput.Clear(); } finally { _syncInput=false; } return; }
        if(!newSession && _inputBehavior.IsComposing) return;
        if(!newSession && DraftController.HasPendingEdits(id)) return;
        if(HomeDraftInput.Text==_viewModel.DraftText) { DraftController.ObserveText(id,HomeDraftInput.Text); return; }
        var caret=newSession ? _viewModel.DraftText.Length : Math.Min(HomeDraftInput.CaretIndex,_viewModel.DraftText.Length);
        _syncInput=true;
        try { HomeDraftInput.Text=_viewModel.DraftText; HomeDraftInput.CaretIndex=caret; DraftController.ObserveText(id,HomeDraftInput.Text); }
        finally { _syncInput=false; }
    }
    private Button Thumbnail(ImageRef image,bool draft)
    {
        var preview=new Image { Width=144,Height=90,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,144,90),9,9) };
        var grid=new Grid { Width=144,Height=90 }; grid.Children.Add(preview);
        if(draft) grid.Children.Add(new TextBlock { Text="×",Background=Brushes.White,Foreground=Brushes.DarkRed,Padding=new Thickness(5,0,5,1),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,FontSize=18 });
        var button=new Button { Content=grid,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(0,3,8,7),ToolTip=draft ? "点击删除待发送图片" : "点击查看原图" };
        var target=_viewModel.Workspace.Selected; var session=_renderedSession;
        button.Click+=async (_,_) => await RunAsync(async () =>
        {
            if(draft) { if(target is not null) await target.RemoveImageAsync(image.Id,CancellationToken.None); }
            else { var bitmap=await LoadBitmapAsync(image,false); var viewer=new ImageViewerWindow(bitmap) { Owner=this }; viewer.Show(); }
        });
        _=RunAsync(async () => { var bitmap=await LoadBitmapAsync(image,true); if(_renderedSession==session) preview.Source=bitmap; });
        return button;
    }
    private async Task<BitmapSource> LoadBitmapAsync(ImageRef image,bool thumbnail)
    {
        if(thumbnail && _thumbnails.TryGetValue(image.Id,out var cached)) return cached;
        var bytes=await _viewModel.ReadImageAsync(image,CancellationToken.None);
        using var stream=new MemoryStream(bytes); var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad;
        if(thumbnail) bitmap.DecodePixelWidth=320;
        bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); if(thumbnail) _thumbnails[image.Id]=bitmap; return bitmap;
    }
}
