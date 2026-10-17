namespace ExamHelper.Core.Audio;
public sealed class RecordingController : IAsyncDisposable
{
    private readonly IMicrophoneRecorder _recorder;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _gate = new(1);
    private readonly object _stopSync = new();
    private ITimer? _timer;
    private Task<RecordedAudio>? _stopTask;
    private long _started;
    private TimeSpan _elapsed;
    private bool _disposed;
    public bool IsRecording { get; private set; }
    public TimeSpan Elapsed => IsRecording
        ? TimeSpan.FromTicks(Math.Min(TimeSpan.FromSeconds(60).Ticks, _clock.GetElapsedTime(_started).Ticks)) : _elapsed;
    public event Action<RecordedAudio>? Stopped;
    public event Action<Exception>? Failed;
    public RecordingController(IMicrophoneRecorder recorder, TimeProvider clock) { _recorder = recorder; _clock = clock; }
    public async Task StartAsync(string deviceId, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (IsRecording) throw new InvalidOperationException("正在录音。");
            await _recorder.StartAsync(deviceId, ct).ConfigureAwait(false);
            lock (_stopSync) _stopTask = null;
            _started = _clock.GetTimestamp(); _elapsed = TimeSpan.Zero; IsRecording = true;
            _timer = _clock.CreateTimer(_ => _ = AutoStopAsync(), null, TimeSpan.FromSeconds(60), Timeout.InfiniteTimeSpan);
        }
        finally { _gate.Release(); }
    }
    public Task<RecordedAudio> StopAsync(CancellationToken ct)
    {
        lock (_stopSync) return (_stopTask ??= StopCoreAsync()).WaitAsync(ct);
    }
    private async Task<RecordedAudio> StopCoreAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        RecordedAudio clip;
        try
        {
            if (!IsRecording) throw new InvalidOperationException("当前没有录音。");
            _elapsed = Elapsed; IsRecording = false; _timer?.Dispose(); _timer = null;
            clip = await _recorder.StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch { await _recorder.CancelAsync().ConfigureAwait(false); throw; }
        finally { _gate.Release(); }
        Stopped?.Invoke(clip);
        return clip;
    }
    private async Task AutoStopAsync()
    {
        try { await StopAsync(CancellationToken.None).ConfigureAwait(false); }
        catch (Exception error) { Failed?.Invoke(error); }
    }
    public async Task CancelAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            _elapsed = Elapsed; IsRecording = false; _timer?.Dispose(); _timer = null;
            await _recorder.CancelAsync().ConfigureAwait(false);
            lock (_stopSync) _stopTask = null;
        }
        finally { _gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return; _disposed = true; await CancelAsync().ConfigureAwait(false);
    }
}