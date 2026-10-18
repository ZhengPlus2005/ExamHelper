namespace ExamHelper.Core.Sessions;
public sealed partial class SessionCoordinator
{
    private ITimer? _draftSaveTimer;
    private long _draftEditedAt;
    private bool _draftNeedsSave;
    private void ScheduleDraftSave()
    {
        _draftNeedsSave = true; _draftEditedAt = _clock.GetTimestamp();
        _draftSaveTimer ??= _clock.CreateTimer(_ => _ = SaveDebouncedDraftAsync(),null,Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan);
        _draftSaveTimer.Change(TimeSpan.FromMilliseconds(500),Timeout.InfiniteTimeSpan);
    }
    private async Task SaveDebouncedDraftAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(_disposed || !_draftNeedsSave) return;
            var remaining = TimeSpan.FromMilliseconds(500) - _clock.GetElapsedTime(_draftEditedAt);
            if(remaining > TimeSpan.Zero) { _draftSaveTimer?.Change(remaining,Timeout.InfiniteTimeSpan); return; }
            await TrySaveLockedAsync().ConfigureAwait(false); StateChanged?.Invoke(_state);
        }
        finally { _gate.Release(); }
    }
    private void DraftSaved() { _draftNeedsSave = false; _draftSaveTimer?.Change(Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan); }
}
