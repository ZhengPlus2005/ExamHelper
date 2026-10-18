using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExamHelper.App.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Overlay;
public sealed class AnswerMessageViewModel(Guid id) : ObservableObject
{
    public Guid Id { get; } = id;
    private string _text = "",_title = "";
    private bool _final,_assistant;
    public bool IsAssistant { get => _assistant; set => Set(ref _assistant,value); }
    public string Text { get => _text; set => Set(ref _text,value); }
    public string Title { get => _title; set => Set(ref _title,value); }
    public bool IsFinal { get => _final; set => Set(ref _final,value); }
}
public sealed class ThumbnailViewModel(Guid id,AsyncDelegateCommand delete) : ObservableObject
{
    public Guid Id { get; } = id;
    public AsyncDelegateCommand DeleteCommand { get; } = delete;
    public int Sequence { get; set; }
    private BitmapSource? _preview;
    public BitmapSource? Preview { get => _preview; set => Set(ref _preview,value); }
}
public sealed class OverlayViewModel : ObservableObject,IDisposable
{
    private readonly SessionCoordinator _coordinator;
    private readonly IImageStore _images;
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(75) };
    private readonly Dictionary<AppCommand,AsyncDelegateCommand> _commands = [];
    private readonly CancellationTokenSource _lifetime = new();
    private SessionSnapshot _latest,_shown;
    private string _draft = "",_status = "";
    private string? _localError;
    private bool _disposed,_visible;
    public ObservableCollection<AnswerMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ThumbnailViewModel> Thumbnails { get; } = [];
    public Guid SessionId => _coordinator.State.Id;
    public ExamHelper.Core.Settings.ModelSelection? Selection => _coordinator.State.Selection;
    public ExamHelper.Core.Settings.ServiceVerification Verification { get; }
    public Task SelectModelAsync(ExamHelper.Core.Settings.ModelSelection selection) => _coordinator.SelectModelAsync(selection,_lifetime.Token);
    public string DraftText { get => _draft; private set => Set(ref _draft,value); }
    public string Status { get => _status; set => Set(ref _status,value); }
    public bool CanUndo => _coordinator.State.SpeechUndoAvailable;
    public bool HasSpeechFailure => _coordinator.State.SpeechState == SpeechState.RetryPending;
    public bool HasRequestFailure => _coordinator.State.PendingRequest is not null;
    public string RecordingLabel => _coordinator.State.SpeechState switch
    {
        SpeechState.Recording => $"停止录音（{_coordinator.RecordingElapsed.TotalSeconds:F0}s）",SpeechState.Recognizing => "正在识别……",
        SpeechState.RetryPending => "录音待重试",_ => "开始录音"
    };
    public OverlayViewModel(SessionCoordinator coordinator,IImageStore images,Func<Task>? newConversation = null,ExamHelper.Core.Settings.ServiceVerification? verification = null)
    {
        Verification=verification ?? new(new byte[32]); _coordinator = coordinator; _images = images; _latest = _shown = coordinator.State;
        foreach(var command in Enum.GetValues<AppCommand>())
            _commands[command] = new(async () => { _localError = null; if(command==AppCommand.NewConversation && newConversation is not null) await newConversation(); else await coordinator.ExecuteAsync(command,CancellationToken.None); },() => coordinator.CanExecute(command),e => ReportError(Explain(e)));
        _timer.Tick += Tick; coordinator.StateChanged += OnState;
        Refresh();
    }
    public AsyncDelegateCommand Command(AppCommand command) => _commands[command];
    private void OnState(SessionSnapshot state) => Interlocked.Exchange(ref _latest,state);
    public void SetVisible(bool visible)
    {
        _visible = visible; if(visible) { Refresh(); _timer.Start(); } else _timer.Stop();
    }
    private async void Tick(object? sender,EventArgs args)
    {
        if(_disposed || !_visible) return;
        try { await _coordinator.RefreshUndoAsync(); Refresh(); Notify(nameof(RecordingLabel)); }
        catch(Exception error) { if(!_disposed) Status = Explain(error); }
    }
    public void Refresh()
    {
        if(_disposed) return;
        var state = _latest;
        if(!ReferenceEquals(state,_shown) || Messages.Count == 0)
        {
            var ids = state.Messages.Select(m => m.Id).ToHashSet();
            for(int i = Messages.Count - 1;i >= 0;i--) if(!ids.Contains(Messages[i].Id)) Messages.RemoveAt(i);
            foreach(var message in state.Messages)
            {
                var view = Messages.FirstOrDefault(m => m.Id == message.Id);
                if(view is null) { view = new(message.Id); Messages.Add(view); }
                view.IsAssistant = message.Role == MessageRole.Assistant; view.Text = message.Text; view.IsFinal = message.Status != MessageStatus.Pending;
                view.Title = (message.Role == MessageRole.User ? "我" : "AI") + (message.Status switch { MessageStatus.Pending => " · 处理中",MessageStatus.Failed => " · 失败",MessageStatus.Stopped => " · 已停止",_ => "" });
            }
            var imageIds = state.Draft.Images.Select(i => i.Id).ToHashSet();
            for(int i = Thumbnails.Count - 1;i >= 0;i--) if(!imageIds.Contains(Thumbnails[i].Id)) Thumbnails.RemoveAt(i);
            for(int i = 0;i < state.Draft.Images.Length;i++)
            {
                var image = state.Draft.Images[i]; var view = Thumbnails.FirstOrDefault(t => t.Id == image.Id);
                if(view is null)
                {
                    var id = image.Id;
                    view = new(id,new(() => _coordinator.RemoveImageAsync(id,_lifetime.Token),() => _coordinator.State.IsAuxiliaryMode,e => ReportError(Explain(e))));
                    Thumbnails.Add(view); _ = LoadThumbnailAsync(view,image);
                }
                view.Sequence = i + 1; view.Notify(nameof(view.Sequence));
            }
            _shown = state; Notify(nameof(Selection)); DraftText = state.Draft.Text; Notify(nameof(CanUndo)); Notify(nameof(HasSpeechFailure)); Notify(nameof(HasRequestFailure));
        }
        foreach(var command in _commands.Values) command.Raise();
        if(_localError is not null) Status = _localError;
        else if(_coordinator.Error is not null) Status = _coordinator.Error;
        else if(state.SpeechState == SpeechState.Recognizing) Status = "识别结果将追加到草稿，不自动发送。";
        else if(state.CaptureInProgress) Status = "正在截取辅助窗口所在屏幕……";
        else if(state.AnswerState == AnswerState.Generating) Status = "AI 正在回答，可继续准备下一份草稿。";
        else Status = state.Draft.HasContent ? "草稿可发送。" : "通过截图或麦克风准备问题。";
    }
    private async Task LoadThumbnailAsync(ThumbnailViewModel view,ImageRef image)
    {
        try
        {
            var bytes = await _images.ReadAsync(image,_lifetime.Token);
            if(_disposed || !Thumbnails.Contains(view)) return;
            using var stream = new MemoryStream(bytes); var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 240; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); view.Preview = bitmap;
        }
        catch(OperationCanceledException) { }
        catch { if(!_disposed) Status = "部分截图无法预览；发送时仍会检查原图。"; }
    }
    public void ReportError(string message) { _localError = message; Status = message; }
    public Task SetUndoHoverAsync(bool hover) => _coordinator.SetUndoHoverAsync(hover);
    public void Dispose()
    {
        if(_disposed) return; _disposed = true; _lifetime.Cancel(); _timer.Stop(); _timer.Tick -= Tick;
        _coordinator.StateChanged -= OnState; _lifetime.Dispose();
    }
}