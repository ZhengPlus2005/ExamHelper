using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExamHelper.App.Images;
using ExamHelper.App.Controls;
using ExamHelper.App.Common;
using ExamHelper.Core.Drafts;
namespace ExamHelper.App.Home;
public partial class HomeWindow
{
    private long _renderedRevision=-1;
    private Guid? _renderedSession;
    private Guid[] _draftImageIds=[];
    private readonly Dictionary<Guid,BitmapSource> _thumbnails=[];
    private readonly Dictionary<Guid,HomeMessageView> _messageViews=[];
    private void Render(bool force)
    {
        var snapshot=_viewModel.Snapshot; var newSession=_renderedSession!=snapshot?.Id;
        SyncInput(newSession);
        if(!force && _renderedRevision==_viewModel.ViewRevision) { UpdateTimings();return; }
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
        if(newSession) { HistoryMessages.Children.Clear();_messageViews.Clear(); }
        var messageIds=_viewModel.Messages.Select(m=>m.Message.Id).ToHashSet();
        foreach(var id in _messageViews.Keys.Where(id=>!messageIds.Contains(id)).ToArray()) { HistoryMessages.Children.Remove(_messageViews[id]);_messageViews.Remove(id); }
        for(var index=0;index<_viewModel.Messages.Count;index++)
        {
            var message=_viewModel.Messages[index].Message;
            if(!_messageViews.TryGetValue(message.Id,out var view))
            { view=new HomeMessageView(image=>Thumbnail(image,false),report:error=>_viewModel.Status=ObservableObject.Explain(error)) { Margin=new Thickness(0,0,0,20) };_messageViews.Add(message.Id,view); }
            view.Update(message);
            if(index>=HistoryMessages.Children.Count || !ReferenceEquals(HistoryMessages.Children[index],view))
            { HistoryMessages.Children.Remove(view);HistoryMessages.Children.Insert(index,view); }
        }
        UpdateTimings();
        var ids=_viewModel.DraftImages.Select(i => i.Id).ToArray();
        if(newSession || !_draftImageIds.SequenceEqual(ids))
        {
            _draftImageIds=ids; HomeDraftPictures.Children.Clear(); foreach(var image in _viewModel.DraftImages) HomeDraftPictures.Children.Add(Thumbnail(image,true));
        }
        var session=_renderedSession;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,() => { if(_renderedSession!=session) return; if(atBottom) HistoryScroll.ScrollToEnd(); else HistoryScroll.ScrollToVerticalOffset(offset); });
    }
    private void UpdateTimings()
    {
        var snapshot=_viewModel.SnapshotForPresentation();if(snapshot is null || snapshot.Id!=_renderedSession)return;
        foreach(var message in snapshot.Messages)if(_messageViews.TryGetValue(message.Id,out var view))view.UpdateTiming(message.Timing);
    }
    private void SyncInput(bool newSession)
    {
        if(_viewModel.SelectedConversationId is not { } id) { _syncInput=true; try { HomeDraftInput.Clear(); } finally { _syncInput=false; } return; }
        if(!newSession && _inputBehavior.IsComposing) return;
        if(!newSession && DraftController.HasPendingEdits(id)) return;
        if(HomeDraftInput.Text==_viewModel.DraftText) { DraftController.ObserveText(id,HomeDraftInput.Text); return; }
        var caret=newSession ? _viewModel.DraftText.Length : Math.Min(HomeDraftInput.CaretIndex,_viewModel.DraftText.Length);
        _syncInput=true;
        try { HomeDraftInput.Text=_viewModel.DraftText; HomeDraftInput.CaretIndex=caret; DraftController.ObserveText(id,HomeDraftInput.Text); }
        finally { _syncInput=false; }
    }
    private FrameworkElement Thumbnail(ImageRef image,bool draft)
    {
        var session=_renderedSession;var target=_viewModel.Workspace.Selected;
        if(draft)
        {
            var tile=new DraftImageTile { Tag=image.Id,Margin=new Thickness(0,3,8,7),
                PreviewCommand=new AsyncDelegateCommand(()=>OpenImageAsync(image),()=>true,error=>_viewModel.Status=Common.ObservableObject.Explain(error)),
                RemoveCommand=new AsyncDelegateCommand(()=>target?.RemoveImageAsync(image.Id,CancellationToken.None) ?? Task.CompletedTask,()=>target is not null,error=>_viewModel.Status=Common.ObservableObject.Explain(error)) };
            _=RunAsync(async()=> { var bitmap=await LoadBitmapAsync(image,true);if(_renderedSession==session)tile.PreviewSource=bitmap; });return tile;
        }
        var preview=new Image { Width=144,Height=90,Stretch=Stretch.UniformToFill,Clip=new RectangleGeometry(new Rect(0,0,144,90),9,9) };
        var frame=new Grid();frame.Children.Add(preview);
        var button=new Button { Content=frame,Padding=new Thickness(0),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(0,3,8,7),ToolTip="查看原图" };
        button.Click+=async(_,_)=>await RunAsync(()=>OpenImageAsync(image));
        _=RunAsync(async()=> { var bitmap=await LoadBitmapAsync(image,true);if(_renderedSession==session)preview.Source=bitmap; });return button;
    }
    private async Task OpenImageAsync(ImageRef image)
    {
        var bytes=await _viewModel.ReadImageAsync(image,CancellationToken.None);
        using var stream=new MemoryStream(bytes);var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();
        var viewer=new ImageViewerWindow(bitmap,bytes) { Owner=this };viewer.Show();
    }
    private async Task<BitmapSource> LoadBitmapAsync(ImageRef image,bool thumbnail)
    {
        if(thumbnail && _thumbnails.TryGetValue(image.Id,out var cached)) return cached;
        var session=_renderedSession;var bytes=await _viewModel.ReadImageAsync(image,CancellationToken.None);
        using var stream=new MemoryStream(bytes); var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad;
        if(thumbnail) bitmap.DecodePixelWidth=320;
        bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze(); if(thumbnail && session==_renderedSession) _thumbnails[image.Id]=bitmap; return bitmap;
    }
}
