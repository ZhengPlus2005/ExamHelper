namespace ExamHelper.App.Lifecycle;
public interface ITrayIconService : IDisposable
{
    bool IsVisible { get; }
    event Action? Activated;
    event Action? ExitRequested;
}
public sealed class TrayActivationDebounce(TimeProvider clock,TimeSpan interval)
{
    private long? _last;
    public bool TryActivate()
    {
        var now=clock.GetTimestamp(); if(_last is { } previous && clock.GetElapsedTime(previous,now)<interval) return false;
        _last=now; return true;
    }
}
