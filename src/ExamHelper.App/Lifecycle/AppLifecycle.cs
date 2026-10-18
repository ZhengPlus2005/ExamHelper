using ExamHelper.App.Home;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Lifecycle;
public sealed class AppLifecycle : IDisposable
{
    private readonly ConversationWorkspace _workspace;
    private readonly HomeViewModel _home;
    private readonly ITrayIconService _tray;
    private readonly Func<Task> _shutdown,_flushEditors;
    private readonly Func<Task<bool>> _closeSettings;
    private readonly Action<string> _report;
    private readonly SemaphoreSlim _gate=new(1);
    private bool _exited,_disposed;
    public bool IsExitCommitted { get; private set; }
    public bool IsExiting { get; private set; }
    public event Action<bool>? ExitingChanged;
    public AppLifecycle(ConversationWorkspace workspace,HomeViewModel home,ITrayIconService tray,Func<Task> shutdown,Action<string> report,Func<Task<bool>>? closeSettings = null,Func<Task>? flushEditors = null)
    {
        _workspace=workspace; _home=home; _tray=tray; _shutdown=shutdown; _report=report;
        _closeSettings=closeSettings ?? (() => Task.FromResult(true)); _flushEditors=flushEditors ?? (() => Task.CompletedTask);
        tray.Activated+=Activated; tray.ExitRequested+=ExitRequested;
    }
    private void Activated() => _=HandleAsync(() => ShowHomeAsync(CancellationToken.None));
    private void ExitRequested() => _=HandleAsync(() => RequestExitAsync(CancellationToken.None));
    private async Task HandleAsync(Func<Task> action) { try { await action(); } catch(Exception error) { _report(Common.ObservableObject.Explain(error)); } }
    public void HideToTray() => _home.HideHome();
    public Task ReturnHomeAsync(CancellationToken ct) => ShowHomeAsync(ct);
    public async Task ShowHomeAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try { if(_exited || _disposed) return; if(_workspace.AuxiliarySessionId is not null) await _home.ReturnedAsync(ct); else _home.ShowHome(); }
        finally { _gate.Release(); }
    }
    public async Task<bool> RequestExitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if(_exited) return true;
            IsExiting=true; ExitingChanged?.Invoke(true);
            if(!await _closeSettings()) return false;
            await _workspace.CancelTasksAsync(ct);
            await _flushEditors();
            await _workspace.ExitAsync(ct);
            IsExitCommitted=true;
            await _shutdown(); _exited=true; Dispose(); return true;
        }
        catch(Exception error)
        {
            IsExitCommitted=false; _report(Common.ObservableObject.Explain(error)+" 请重试退出。"); _home.ShowHome(); return false;
        }
        finally { IsExiting=false; ExitingChanged?.Invoke(false); _gate.Release(); }
    }
    public void Dispose()
    {
        if(_disposed) return; _disposed=true; _tray.Activated-=Activated; _tray.ExitRequested-=ExitRequested; _tray.Dispose();
    }
}
