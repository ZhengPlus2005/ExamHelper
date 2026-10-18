namespace ExamHelper.Core.Settings;
// Publish refreshes only against the exact immutable settings instance read.
public sealed class SettingsState(AppSettings? initial = null)
{
    private AppSettings _current=initial ?? new();
    public AppSettings Current => Volatile.Read(ref _current);
    public void Publish(AppSettings settings) => Volatile.Write(ref _current,settings);
    public AppSettings PublishRefresh(AppSettings original,AppSettings refreshed)
    { Interlocked.CompareExchange(ref _current,refreshed,original); return Current; }
}
