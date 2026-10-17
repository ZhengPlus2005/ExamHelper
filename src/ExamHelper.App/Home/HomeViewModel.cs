using System.Collections.ObjectModel;
using ExamHelper.App.Common;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.App.Home;
public sealed record HistoryImagePreview(Guid Id,byte[] Bytes,int Width,int Height);
public sealed class HomeViewModel(SessionCoordinator coordinator,ISessionStore sessions,IImageStore images,
    Func<Task<AppSettings>> getSettings,ServiceVerification verification,IOverlayHost overlay,Func<Task> reconcile) : ObservableObject
{
    private bool _busy,_visible = true,_ready;
    private string _status = "",_historyText = "选择一个历史对话以查看内容。";
    private Guid? _selected;
    public ObservableCollection<SessionSummary> History { get; } = [];
    public ObservableCollection<HistoryImagePreview> HistoryImages { get; } = [];
    public bool IsBusy { get => _busy; private set => Set(ref _busy,value); }
    public bool HomeVisible { get => _visible; private set { if(Set(ref _visible,value)) VisibilityChanged?.Invoke(value); } }
    public string Status { get => _status; set => Set(ref _status,value); }
    public string HistoryText { get => _historyText; private set => Set(ref _historyText,value); }
    public bool AreServicesVerified { get => _ready; private set => Set(ref _ready,value); }
    public event Action<bool>? VisibilityChanged;
    public async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            var list = await sessions.ListAsync(ct); History.Clear(); foreach(var item in list) History.Add(item);
            AreServicesVerified = verification.CanStart(await getSettings());
        }
        catch(Exception error) { Status = Explain(error); }
    }
    public Task StartAsync(CancellationToken ct) => EnterAsync(null,ct);
    public Task ContinueAsync(Guid id,CancellationToken ct) => EnterAsync(id,ct);
    private async Task EnterAsync(Guid? id,CancellationToken ct)
    {
        if(IsBusy || !HomeVisible) return;
        IsBusy = true; Guid? created = null;
        try
        {
            var settings = await getSettings(); AreServicesVerified = verification.CanStart(settings);
            if(!AreServicesVerified) { Status = "请先在设置中通过 AI 图片测试和语音识别测试。"; return; }
            var protection = await overlay.OpenAsync(ct);
            if(!protection.Success) { overlay.Close(); Status = $"捕获保护初始化失败（代码 {protection.ErrorCode}，读回 0x{protection.EffectiveAffinity:X}）。"; return; }
            if(id.HasValue) await coordinator.ContinueAsync(id.Value,ct);
            else { await coordinator.StartNewAsync(ct); created = coordinator.State.Id; }
            overlay.Show(); HomeVisible = false; Status = "";
        }
        catch(Exception error)
        {
            overlay.Close(); HomeVisible = true; Status = Explain(error);
            if(created.HasValue && coordinator.State.Messages.Length == 0)
            {
                try { await coordinator.ReturnHomeAsync(CancellationToken.None); await sessions.DeleteAsync(created.Value,CancellationToken.None); } catch { }
            }
        }
        finally { IsBusy = false; }
    }
    public async Task SelectAsync(Guid id,CancellationToken ct)
    {
        _selected = id;
        try
        {
            var snapshot = await sessions.LoadAsync(id,ct); if(_selected != id || snapshot is null) return;
            HistoryText = string.Join(Environment.NewLine + Environment.NewLine,snapshot.Messages.Select(m =>
                (m.Role == MessageRole.User ? "我" : "AI") + (m.Status == MessageStatus.Complete ? "" : $"（{StatusName(m.Status)}）") + Environment.NewLine + m.Text)) +
                (snapshot.Draft.HasContent ? Environment.NewLine + Environment.NewLine + "未发送草稿：" + Environment.NewLine + snapshot.Draft.Text : "");
            HistoryImages.Clear();
            foreach(var image in snapshot.Messages.SelectMany(m => m.Images).Concat(snapshot.Draft.Images).DistinctBy(i => i.Id))
            {
                try
                {
                    var bytes = await images.ReadAsync(image,ct); if(_selected != id) return;
                    HistoryImages.Add(new(image.Id,bytes,image.Width,image.Height));
                }
                catch(Exception error) when(error is not OperationCanceledException) { Status = "部分历史图片无法读取，文字记录仍可查看。"; }
            }
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { Status = Explain(error); }
    }
    public async Task DeleteAsync(Guid id,Func<Task<bool>> confirm,CancellationToken ct)
    {
        if(IsBusy || !HomeVisible || !await confirm()) return;
        IsBusy = true;
        try
        {
            await sessions.DeleteAsync(id,ct);
            try { await reconcile(); Status = "对话及不再被引用的图片已删除。"; }
            catch { Status = "对话已删除；图片清理未完成，重启后将重试。"; }
            if(_selected == id) { _selected = null; HistoryText = ""; HistoryImages.Clear(); }
            await RefreshAsync(ct);
        }
        catch(Exception error) { Status = Explain(error); }
        finally { IsBusy = false; }
    }
    public async Task ReturnedAsync(CancellationToken ct) { HomeVisible = true; await RefreshAsync(ct); }
    private static string StatusName(MessageStatus status) => status switch { MessageStatus.Pending => "未完成",MessageStatus.Failed => "失败",MessageStatus.Stopped => "已停止",_ => "完成" };
}