namespace ExamHelper.Core.Drafts;
public sealed class SpeechInsertionUndo : IDisposable
{
    private readonly DraftBuffer _draft;
    private readonly TimeProvider _clock;
    private TimeSpan _remaining;
    private long _last;
    private string? _suffix;
    private bool _paused, _changing, _disposed;
    public SpeechInsertionUndo(DraftBuffer draft, TimeProvider clock)
    {
        _draft = draft; _clock = clock; draft.Changed += OnDraftChanged;
    }
    public TimeSpan Remaining { get { Refresh(); return _remaining; } }
    public bool CanUndo => !_disposed && _suffix is not null && Remaining > TimeSpan.Zero;
    public void Append(Guid operationId, string transcript)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (operationId == Guid.Empty) throw new ArgumentException("缺少录音操作标识。", nameof(operationId));
        if (string.IsNullOrWhiteSpace(transcript)) return;
        Invalidate();
        var text = _draft.Snapshot().Text;
        var prefix = text.Length > 0 && !text.EndsWith('\n') && !text.EndsWith('\r') ? Environment.NewLine : "";
        var suffix = prefix + transcript;
        _changing = true;
        try { _draft.AppendText(suffix); } finally { _changing = false; }
        _suffix = suffix; _remaining = TimeSpan.FromSeconds(3); _last = _clock.GetTimestamp();
    }
    public void PointerEnter()
    {
        Refresh(); if (_suffix is not null) _paused = true;
    }
    public void PointerLeave()
    {
        Refresh(); _paused = false; _last = _clock.GetTimestamp();
    }
    public bool TryUndo()
    {
        if (!CanUndo) return false;
        var suffix = _suffix!;
        Invalidate();
        return _draft.RemoveSpeechTail(suffix);
    }
    public void Invalidate()
    {
        _suffix = null; _remaining = TimeSpan.Zero; _paused = false;
    }
    private void Refresh()
    {
        if (_suffix is null) return;
        var now = _clock.GetTimestamp();
        if (!_paused) _remaining -= _clock.GetElapsedTime(_last, now);
        _last = now;
        if (_remaining <= TimeSpan.Zero) Invalidate();
    }
    private void OnDraftChanged(DraftChange change)
    {
        if (!_changing && change.Kind is DraftChangeKind.TextAppended or DraftChangeKind.Cleared or DraftChangeKind.Restored or DraftChangeKind.SpeechUndone)
            Invalidate();
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _draft.Changed -= OnDraftChanged; Invalidate();
    }
}