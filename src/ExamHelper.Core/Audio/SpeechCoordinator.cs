using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Audio;
public sealed record SpeechTranscript(Guid SessionId, long Generation, Guid OperationId, string Transcript);
public sealed class SpeechCoordinator : IAsyncDisposable
{
    private readonly RecordingController _recording;
    private readonly IAsrClient _asr;
    private readonly Func<AsrSettings> _settings;
    private readonly Func<(Guid SessionId, long Generation)> _context;
    private readonly SemaphoreSlim _gate = new(1);
    private CancellationTokenSource? _operation;
    private Action<RecordedAudio>? _stoppedHandler;
    private Action<Exception>? _failedHandler;
    private Attempt? _pending;
    private long _epoch;
    private bool _disposed;
    private Task _processing = Task.CompletedTask;
    public SpeechState State { get; private set; }
    public string? Error { get; private set; }
    public event Action? StateChanged;
    public event Action<SpeechTranscript>? TranscriptReady;
    public SpeechCoordinator(RecordingController recording, IAsrClient asr, Func<AsrSettings> settings, Func<(Guid SessionId,long Generation)> context)
    { _recording = recording; _asr = asr; _settings = settings; _context = context; }

    public async Task ToggleAsync(CancellationToken ct, (Guid SessionId,long Generation)? expectedContext = null,bool waitForRecognition = true)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var dispatchContext = expectedContext ?? _context();
        bool stop = false; long stopEpoch = 0;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed || dispatchContext != _context()) return;
            if (State == SpeechState.Idle)
            {
                Error = null; var epoch = ++_epoch; var context = dispatchContext; var settings = _settings();
                _operation?.Dispose(); _operation = CancellationTokenSource.CreateLinkedTokenSource(ct);
                var operationToken = _operation.Token;
                _stoppedHandler = clip => _processing = ProcessAsync(new(clip, epoch, context, settings), operationToken);
                _failedHandler = error => _ = RecordingFailedAsync(error, epoch);
                _recording.Stopped += _stoppedHandler; _recording.Failed += _failedHandler;
                try { await _recording.StartAsync(settings.DeviceId, _operation.Token).ConfigureAwait(false); State = SpeechState.Recording; }
                catch { Detach(); throw; }
                StateChanged?.Invoke();
            }
            else if (State == SpeechState.Recording) { State = SpeechState.Recognizing; StateChanged?.Invoke(); stop = true; stopEpoch = _epoch; }
            else throw new InvalidOperationException("请先处理当前识别或待重试录音。");
        }
        finally { _gate.Release(); }
        if (stop)
        {
            try { await _recording.StopAsync(ct).ConfigureAwait(false); if(waitForRecognition) await _processing.ConfigureAwait(false); }
            catch (Exception error) { await RecordingFailedAsync(error, stopEpoch).ConfigureAwait(false); }
        }
    }

    public async Task RetryAsync(CancellationToken ct, (Guid SessionId,long Generation)? expectedContext = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var dispatchContext = expectedContext ?? _context();
        Attempt pending;
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_disposed || dispatchContext != _context()) return;
            if (State != SpeechState.RetryPending || _pending is null) throw new InvalidOperationException("没有可重试的录音。");
            pending = _pending; State = SpeechState.Recognizing; Error = null; StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
        _processing = ProcessAsync(pending, ct);
        await _processing.ConfigureAwait(false);
    }
    public Task DiscardAsync((Guid SessionId,long Generation)? expectedContext = null) => CancelAsync(expectedContext ?? _context());

    private async Task ProcessAsync(Attempt attempt, CancellationToken ct)
    {
        CancellationToken operationToken;
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Current(attempt)) return;
            _pending = attempt; State = SpeechState.Recognizing; Error = null;
            operationToken = _operation?.Token ?? CancellationToken.None;
            StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, operationToken);
        string transcript;
        try
        {
            transcript = await _asr.TranscribeAsync(attempt.Clip, attempt.Settings, linked.Token).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(transcript)) throw new ServiceFailure(FailureCategory.InvalidPayload, "未识别到文字。");
        }
        catch (Exception error)
        {
            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!Current(attempt)) return;
                State = SpeechState.RetryPending;
                Error = error is ServiceFailure ? error.Message : "语音识别未完成，请手动重试或丢弃录音。";
                StateChanged?.Invoke();
            }
            finally { _gate.Release(); }
            return;
        }
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!Current(attempt)) return;
            await _recording.CancelAsync().ConfigureAwait(false);
            _pending = null; State = SpeechState.Idle; Error = null; Detach();
            TranscriptReady?.Invoke(new(attempt.Context.SessionId, attempt.Context.Generation, attempt.Clip.OperationId, transcript));
            StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }

    private bool Current(Attempt attempt) => attempt.Epoch == _epoch && attempt.Context == _context() && _operation?.IsCancellationRequested != true;
    private async Task RecordingFailedAsync(Exception error, long epoch)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (epoch != _epoch) return;
            ++_epoch; _operation?.Cancel(); Detach(); _pending = null; State = SpeechState.Idle;
            try { await _recording.CancelAsync().ConfigureAwait(false); }
            catch { }
            Error = error is ServiceFailure ? error.Message : "录音已中断，请检查麦克风后重新录音。";
            StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }
    public async Task CancelAsync((Guid SessionId,long Generation)? expectedContext = null)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (expectedContext is { } expected && expected != _context()) return;
            ++_epoch; _operation?.Cancel(); Detach();
            await _recording.CancelAsync().ConfigureAwait(false);
            _pending = null; State = SpeechState.Idle; Error = null; StateChanged?.Invoke();
        }
        finally { _gate.Release(); }
    }
    private void Detach()
    {
        if (_stoppedHandler is not null) _recording.Stopped -= _stoppedHandler;
        if (_failedHandler is not null) _recording.Failed -= _failedHandler;
        _stoppedHandler = null; _failedHandler = null;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true; await CancelAsync().ConfigureAwait(false); _operation?.Dispose();
    }
    private sealed record Attempt(RecordedAudio Clip, long Epoch, (Guid SessionId,long Generation) Context, AsrSettings Settings);
}