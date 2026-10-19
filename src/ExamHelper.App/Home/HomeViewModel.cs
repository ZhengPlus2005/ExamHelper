using System.Collections.Immutable;
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
    private bool _busy,_visible = true,_refreshing;
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
    public bool CanSend => !IsBusy && Workspace.Selected?.CanExecute(AppCommand.Send) == true;
    public bool CanStop => Workspace.GeneratingSessionId is not null;
    public bool CanUndoSpeech => Snapshot?.SpeechUndoAvailable == true;
    public bool HasFailedSpeech => Snapshot?.SpeechState == SpeechState.RetryPending;
    public bool HasFailedRequest => Snapshot?.PendingRequest is not null;
    public long ViewRevision { get; private set; }
    public bool HistoryNeedsRefresh { get; private set; }
    public HistoryCollection History { get; } = [];
    public bool IsBusy { get => _busy; private set { if(Set(ref _busy,value)) { Notify(nameof(CanEditHistory)); Notify(nameof(CanEditDraft)); Notify(nameof(CanSend)); Notify(nameof(CanDeleteSelected)); } } }
    public bool CanEditHistory=>!IsBusy;
    public bool CanEditDraft=>CanEditHistory && SelectedConversationId is not null;
    public HistorySelectionController HistorySelection { get; }
    public bool CanDeleteSelected=>!IsBusy && HistorySelection.IsBatchMode && HistorySelection.SelectedCount>0;
    private bool _cleanupFailed;
    public bool HasImageCleanupFailure { get => _cleanupFailed; private set => Set(ref _cleanupFailed,value); }
    public bool HomeVisible { get => _visible; private set { if(Set(ref _visible,value)) VisibilityChanged?.Invoke(value); } }
    public string Status { get => _status; set => Set(ref _status,value); }
    public ServiceVerification Verification => _verification;
    public event Action<bool>? VisibilityChanged;
    public HomeViewModel(ConversationWorkspace workspace,ISessionStore sessions,IImageStore images,Func<Task<AppSettings>> getSettings,
        ServiceVerification verification,IOverlayHost overlay,Func<Task> reconcile,Func<Task<AppSettings>>? entrySettings = null)
    {
        HistorySelection=new(History); HistorySelection.PropertyChanged+=(_,_)=>Notify(nameof(CanDeleteSelected));
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
    public SessionSnapshot? SnapshotForPresentation()=>Workspace.Selected?.SnapshotForPresentation();
    public async Task RefreshAsync(CancellationToken ct)
    {
        if(_refreshing) return; _refreshing = true;
        try
        {
            SettingsSnapshot = await _getSettings();
            var list = await _sessions.ListAsync(ct); History.Apply(list);
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
            var settings = await _entrySettings(); SettingsSnapshot = settings;
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
    public async Task RenameAsync(Guid id,string title,CancellationToken ct)
    { await Workspace.RenameAsync(id,title,ct); await RefreshAsync(ct); UpdateSelection(); }
    public async Task DeleteAsync(Guid id,Func<Task<bool>> confirm,CancellationToken ct)
    { await DeleteManyAsync([id],_=>confirm(),ct); }
    public async Task<BatchDeleteResult> DeleteManyAsync(IReadOnlyCollection<Guid> ids,Func<int,Task<bool>> confirm,CancellationToken ct)
    {
        var targets=ids.Distinct().ToArray(); if(IsBusy || !HomeVisible || targets.Length==0) return BatchDeleteResult.Empty;
        IsBusy=true; var result=BatchDeleteResult.Empty;
        try
        {
            if(!await confirm(targets.Length)) return result;
            var current=Workspace.Selected?.State.Id; result=await Workspace.DeleteManyAsync(targets,ct);
            if(current is { } id && result.DeletedIds.Contains(id) && Workspace.Selected is null)
            {
                var remaining=await _sessions.ListAsync(CancellationToken.None);
                if(remaining.Count>0) await Workspace.OpenAsync(remaining[0].Id,CancellationToken.None); else await Workspace.NewAsync(false,CancellationToken.None);
            }
            Status=$"已删除 {result.DeletedIds.Length} 条对话。";
            if(result.Failures.Count>0) Status+=" 未删除："+string.Join("；",result.Failures.Select(p=>$"{History.FirstOrDefault(i=>i.Id==p.Key)?.Title ?? p.Key.ToString()}：{p.Value}"));
            if(result.CleanupFailures.Count>0) Status+=" 记录已删除，但部分资源释放失败："+string.Join("；",result.CleanupFailures.Values);
            if(result.DeletedIds.Length>0)
            {
                try { await _reconcile(); HasImageCleanupFailure=false; }
                catch(Exception error) { HasImageCleanupFailure=true; Status+=" 图片清理未完成："+Explain(error); }
            }
            await RefreshAsync(CancellationToken.None); UpdateSelection();
            if(HistorySelection.IsBatchMode) foreach(var failed in result.Failures.Keys) HistorySelection.SetChecked(failed,true);
            return result;
        }
        catch(Exception error) { Status=Explain(error); await RefreshAsync(CancellationToken.None); UpdateSelection(); return result; }
        finally { IsBusy=false; }
    }
    public async Task RetryImageCleanupAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); if(IsBusy) return; IsBusy=true;
        try { await _reconcile(); HasImageCleanupFailure=false; Status="图片清理已完成。"; }
        catch(Exception error) { HasImageCleanupFailure=true; Status="图片清理未完成："+Explain(error); }
        finally { IsBusy=false; }
    }
    public async Task ReturnedAsync(CancellationToken ct)
    { try { await _overlay.SaveGeometryAsync(); } catch(Exception e) { Status=Explain(e); } await Workspace.ReturnHomeAsync(ct); _overlay.Close(); HomeVisible = true; await RefreshAsync(ct); UpdateSelection(); }
    public void ShowHome() { HomeVisible = true; VisibilityChanged?.Invoke(true); }
    public void HideHome() => HomeVisible = false;
}
