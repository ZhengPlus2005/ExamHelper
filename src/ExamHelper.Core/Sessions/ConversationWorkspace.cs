using ExamHelper.Core.Drafts;
namespace ExamHelper.Core.Sessions;
public sealed class ConversationWorkspace(ISessionStore store,Func<SessionCoordinator> factory) : IAsyncDisposable
{
    private bool _closed;
    private readonly SemaphoreSlim _navigation = new(1);
    private readonly Dictionary<Guid,SessionCoordinator> _sessions = [];
    public SessionCoordinator? Selected { get; private set; }
    public Guid? AuxiliarySessionId { get; private set; }
    public Guid? GeneratingSessionId => Sessions.FirstOrDefault(s => s.State.AnswerState == AnswerState.Generating)?.State.Id;
    public IReadOnlyList<SessionCoordinator> Sessions { get { lock(_sessions) return _sessions.Values.ToArray(); } }
    public event Action? Changed;
    public async Task<SessionCoordinator> NewAuxiliaryConversationAsync(CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closed,this);
            var previous=AuxiliarySessionId is { } id ? Find(id) : null;
            if(previous is null || !previous.CanExecute(Interaction.AppCommand.NewConversation)) throw new InvalidOperationException("请先处理当前草稿和任务。");
            await previous.FlushAsync(ct).ConfigureAwait(false); var runtime=factory();
            try { await runtime.StartNewAsync(ct).ConfigureAwait(false); await previous.SetAuxiliaryModeAsync(false,ct).ConfigureAwait(false); }
            catch { await runtime.DisposeAsync().ConfigureAwait(false); try { await previous.SetAuxiliaryModeAsync(true,CancellationToken.None).ConfigureAwait(false); } catch { } throw; }
            Attach(runtime); Selected=runtime; AuxiliarySessionId=runtime.State.Id; Changed?.Invoke(); return runtime;
        }
        finally { _navigation.Release(); }
    }
    public async Task<SessionCoordinator> NewAsync(bool auxiliary,CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closed,this);
            if(auxiliary && AuxiliarySessionId is not null) throw new InvalidOperationException("辅助对话已打开。");
            if(Selected is { } previous) await previous.FlushAsync(ct).ConfigureAwait(false);
            var runtime = factory();
            try { await runtime.StartNewAsync(ct).ConfigureAwait(false); if(!auxiliary) await runtime.SetAuxiliaryModeAsync(false,ct).ConfigureAwait(false); }
            catch { await runtime.DisposeAsync().ConfigureAwait(false); throw; }
            Attach(runtime); Selected = runtime; if(auxiliary) AuxiliarySessionId = runtime.State.Id;
            Changed?.Invoke(); return runtime;
        }
        finally { _navigation.Release(); }
    }
    public async Task<SessionCoordinator> OpenAsync(Guid id,CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closed,this);
            if(Selected is { } previous) await previous.FlushAsync(ct).ConfigureAwait(false);
            SessionCoordinator? runtime; lock(_sessions) _sessions.TryGetValue(id,out runtime);
            if(runtime is null)
            {
                runtime = factory();
                try { await runtime.ContinueAsync(id,ct).ConfigureAwait(false); await runtime.SetAuxiliaryModeAsync(false,ct).ConfigureAwait(false); }
                catch { await runtime.DisposeAsync().ConfigureAwait(false); throw; }
                Attach(runtime);
            }
            Selected = runtime; Changed?.Invoke(); return runtime;
        }
        finally { _navigation.Release(); }
    }
    private void Attach(SessionCoordinator coordinator)
    {
        lock(_sessions) _sessions.Add(coordinator.State.Id,coordinator);
        coordinator.StateChanged += SessionChanged;
    }
    private void SessionChanged(SessionSnapshot _) => Changed?.Invoke();
    public SessionCoordinator? Find(Guid id) { lock(_sessions) return _sessions.GetValueOrDefault(id); }
    public async Task ReturnHomeAsync(CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(_closed) return;
            if(AuxiliarySessionId is not { } id || Find(id) is not { } runtime) return;
            await runtime.ReturnHomeAsync(ct,false).ConfigureAwait(false);
            Selected = runtime; AuxiliarySessionId = null; Changed?.Invoke();
        }
        finally { _navigation.Release(); }
    }
    public async Task FlushAllAsync(CancellationToken ct)
    { foreach(var runtime in Sessions) await runtime.FlushAsync(ct).ConfigureAwait(false); }
    public async Task CancelTasksAsync(CancellationToken ct)
    {
        var errors=new List<Exception>();
        foreach(var runtime in Sessions) { try { await runtime.CancelTasksAsync(ct).ConfigureAwait(false); } catch(Exception e) { errors.Add(e); } }
        if(errors.Count>0) throw new Common.ServiceFailure(Common.FailureCategory.Storage,"停止任务未完成，请检查后重试退出。",inner:new AggregateException(errors));
    }
    public async Task ExitAsync(CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(_closed) return;
            var errors = new List<Exception>();
            foreach(var runtime in Sessions)
            { try { await runtime.StopAndSaveAsync(ct).ConfigureAwait(false); } catch(Exception e) { errors.Add(e); } }
            if(errors.Count > 0) throw new Common.ServiceFailure(Common.FailureCategory.Storage,"部分对话保存失败，软件将继续运行，请重试退出。",inner: new AggregateException(errors));
            _closed=true; AuxiliarySessionId = null; Changed?.Invoke();
        }
        finally { _navigation.Release(); }
    }
    public async Task DeleteAsync(Guid id,CancellationToken ct)
    {
        await _navigation.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_closed,this);
            if(Find(id) is { } runtime)
            {
                await runtime.StopAndSaveAsync(ct).ConfigureAwait(false); runtime.StateChanged -= SessionChanged;
                await runtime.DisposeAsync().ConfigureAwait(false); lock(_sessions) _sessions.Remove(id);
            }
            await store.DeleteAsync(id,ct).ConfigureAwait(false);
            if(Selected?.State.Id == id) Selected = null; if(AuxiliarySessionId == id) AuxiliarySessionId = null;
            Changed?.Invoke();
        }
        finally { _navigation.Release(); }
    }
    public IReadOnlySet<Guid> LiveImageIds() => Sessions.SelectMany(s => s.State.Draft.Images.Concat(s.State.Messages.SelectMany(m => m.Images)).Concat(s.State.Requests.SelectMany(r => r.Images))).Select(i => i.Id).ToHashSet();
    public async ValueTask DisposeAsync()
    {
        _closed=true;
        foreach(var runtime in Sessions) { runtime.StateChanged -= SessionChanged; await runtime.DisposeAsync().ConfigureAwait(false); }
        lock(_sessions) _sessions.Clear();
    }
}
