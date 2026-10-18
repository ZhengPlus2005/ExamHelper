using System.Collections.ObjectModel;
using ExamHelper.App.Common;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Home;
public sealed class HomeViewModel : ObservableObject
{
    private readonly ISessionStore _sessions;
    private readonly IImageStore _images;
    private readonly Func<Task<AppSettings>> _getSettings,_entrySettings;
    private readonly ServiceVerification _verification;
    private readonly IOverlayHost _overlay;
    private readonly Func<Task> _reconcile;
    private bool _busy,_visible = true,_ready,_refreshing;
    private string _status = "";
    public Action<Action> Dispatch { get; set; } = action => action();
    public IImageStore ImageStore => _images;
    public ConversationWorkspace Workspace { get; }
    public SessionSnapshot? Snapshot { get; private set; }
    public AppSettings SettingsSnapshot { get; private set; } = new();
    public Guid? SelectedConversationId => Snapshot?.Id;
    public string ConversationTitleText => Snapshot is { } s ? ConversationTitle.Get(s) : "新对话";
    public IReadOnlyList<ConversationMessageViewModel> Messages { get; private set; } = [];
    public IReadOnlyList<ImageRef> DraftImages => Snapshot?.Draft.Images.ToArray() ?? [];
    public string DraftText => Snapshot?.Draft.Text ?? "";
    public bool CanSend => Workspace.Selected?.CanExecute(AppCommand.Send) == true;
    public bool CanStop => Workspace.GeneratingSessionId is not null;
    public bool CanUndoSpeech => Snapshot?.SpeechUndoAvailable == true;
    public bool HasFailedSpeech => Snapshot?.SpeechState == SpeechState.RetryPending;
    public bool HasFailedRequest => Snapshot?.PendingRequest is not null;
    public long ViewRevision { get; private set; }
    public bool HistoryNeedsRefresh { get; private set; }
    public ObservableCollection<SessionSummary> History { get; } = [];
    public bool IsBusy { get => _busy; private set => Set(ref _busy,value); }
    public bool HomeVisible { get => _visible; private set { if(Set(ref _visible,value)) VisibilityChanged?.Invoke(value); } }
    public string Status { get => _status; set => Set(ref _status,value); }
    public bool AreServicesVerified { get => _ready; private set => Set(ref _ready,value); }
    public ServiceVerification Verification => _verification;
    public event Action<bool>? VisibilityChanged;
    public HomeViewModel(ConversationWorkspace workspace,ISessionStore sessions,IImageStore images,Func<Task<AppSettings>> getSettings,
        ServiceVerification verification,IOverlayHost overlay,Func<Task> reconcile,Func<Task<AppSettings>>? entrySettings = null)
    {
        Workspace = workspace; _sessions = sessions; _images = images; _getSettings = getSettings; _entrySettings = entrySettings ?? getSettings;
        _verification = verification; _overlay = overlay; _reconcile = reconcile;
        Workspace.Changed += () => Dispatch(UpdateSelection); UpdateSelection();
    }
    private void UpdateSelection()
    {
        Snapshot = Workspace.Selected?.State; Messages = Snapshot?.Messages.Select(m => new ConversationMessageViewModel(m)).ToArray() ?? [];
        if(Workspace.Selected?.Error is { } error) Status = error;
        ViewRevision++; HistoryNeedsRefresh = true; Notify();
    }
    public async Task RefreshAsync(CancellationToken ct)
    {
        if(_refreshing) return; _refreshing = true;
        try
        {
            SettingsSnapshot = await _getSettings(); AreServicesVerified = _verification.CanStart(SettingsSnapshot);
            var list = await _sessions.ListAsync(ct); History.Clear(); foreach(var item in list) History.Add(item);
            HistoryNeedsRefresh = false; ViewRevision++; Notify();
        }
        catch(Exception error) { Status = Explain(error); }
        finally { _refreshing = false; }
    }
    public async Task NewConversationAsync(CancellationToken ct)
    {
        if(IsBusy) return; IsBusy = true;
        try { await Workspace.NewAsync(false,ct); await RefreshAsync(ct); UpdateSelection(); }
        catch(Exception error) { Status = Explain(error); }
        finally { IsBusy = false; }
    }
    public async Task StartAsync(CancellationToken ct)
    {
        if(IsBusy || !HomeVisible) return; IsBusy = true;
        var previous = Workspace.Selected?.State.Id; SessionCoordinator? auxiliary = null;
        try
        {
            var settings = await _entrySettings(); SettingsSnapshot = settings; AreServicesVerified = _verification.CanStart(settings);
            if(!AreServicesVerified) { Status = "请先在设置中通过默认模型的图片测试和语音识别测试。"; return; }
            auxiliary = await Workspace.NewAsync(true,ct); _overlay.Bind(auxiliary);
            var protection = await _overlay.OpenAsync(ct);
            if(!protection.Success) throw new InvalidOperationException($"捕获保护初始化失败（代码 {protection.ErrorCode}，读回 0x{protection.EffectiveAffinity:X}）。");
            _overlay.Show(); HomeVisible = false; Status = "";
        }
        catch(Exception error)
        {
            _overlay.Close(); HomeVisible = true; Status = Explain(error);
            if(auxiliary is not null)
            {
                try { await auxiliary.AbortEntryAsync(CancellationToken.None); await Workspace.ReturnHomeAsync(CancellationToken.None); if(previous is { } id) await Workspace.OpenAsync(id,CancellationToken.None); }
                catch(Exception saveError) { Status += Environment.NewLine + Explain(saveError); }
            }
        }
        finally { IsBusy = false; UpdateSelection(); }
    }
    public async Task SelectAsync(Guid id,CancellationToken ct)
    {
        try { await Workspace.OpenAsync(id,ct); UpdateSelection(); }
        catch(OperationCanceledException) { }
        catch(Exception error) { Status = Explain(error); UpdateSelection(); }
    }
    public Task SetDraftTextAsync(string text,CancellationToken ct) => (Workspace.Selected ?? throw new InvalidOperationException("请先选择对话。")).UpdateDraftTextAsync(text,ct);
    public async Task SendAsync(CancellationToken ct)
    {
        await (Workspace.Selected ?? throw new InvalidOperationException("请先选择对话。")).ExecuteAsync(AppCommand.Send,ct);
        await RefreshAsync(ct); UpdateSelection();
    }
    public async Task StopAsync(CancellationToken ct)
    { if(Workspace.GeneratingSessionId is { } id && Workspace.Find(id) is { } runtime) await runtime.ExecuteAsync(AppCommand.StopAnswer,ct); }
    public async Task ExecuteSelectedAsync(AppCommand command,CancellationToken ct)
    { if(Workspace.Selected is { } selected) await selected.ExecuteAsync(command,ct); }
    public Task SelectModelAsync(ModelSelection selection,CancellationToken ct) => Workspace.Selected?.SelectModelAsync(selection,ct) ?? Task.CompletedTask;
    public Task<byte[]> ReadImageAsync(ImageRef image,CancellationToken ct) => _images.ReadAsync(image,ct);
    public async Task RenameAsync(string title,CancellationToken ct)
    { if(Workspace.Selected is { } selected) { await selected.RenameAsync(title,ct); await RefreshAsync(ct); } }
    public async Task DeleteAsync(Guid id,Func<Task<bool>> confirm,CancellationToken ct)
    {
        if(IsBusy || !HomeVisible || !await confirm()) return; IsBusy = true;
        try
        {
            await Workspace.DeleteAsync(id,ct); await _reconcile(); Status = "对话及不再被引用的图片已删除。";
            await RefreshAsync(ct); UpdateSelection();
        }
        catch(Exception error) { Status = Explain(error); }
        finally { IsBusy = false; }
    }
    public async Task ReturnedAsync(CancellationToken ct)
    { await Workspace.ReturnHomeAsync(ct); _overlay.Close(); HomeVisible = true; await RefreshAsync(ct); UpdateSelection(); }
    public void ShowHome() { HomeVisible = true; VisibilityChanged?.Invoke(true); }
    public void HideHome() => HomeVisible = false;
}
