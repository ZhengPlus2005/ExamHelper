using ExamHelper.Core.Sessions;
namespace ExamHelper.Core.Audio;
// Each wrapper owns a different recorder and its temporary clips. The shared lease
// covers only physical capture; an older recognition job retains its own clip.
public sealed class ExclusiveMicrophoneRecorder(IMicrophoneRecorder recorder,AiGenerationGate captureGate) : IMicrophoneRecorder
{
    private IDisposable? _lease;
    private readonly SemaphoreSlim _gate = new(1);
    public async Task StartAsync(string deviceId,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if(_lease is not null) throw new InvalidOperationException("正在录音。");
            _lease = captureGate.TryAcquire(Guid.NewGuid()) ?? throw new InvalidOperationException("另一个窗口正在录音，请先停止。");
            try { await recorder.StartAsync(deviceId,ct).ConfigureAwait(false); }
            catch { _lease.Dispose(); _lease = null; throw; }
        }
        finally { _gate.Release(); }
    }
    public async Task<RecordedAudio> StopAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await recorder.StopAsync(ct).ConfigureAwait(false); }
        finally { _lease?.Dispose(); _lease = null; _gate.Release(); }
    }
    public async Task CancelAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try { await recorder.CancelAsync().ConfigureAwait(false); }
        finally { _lease?.Dispose(); _lease = null; _gate.Release(); }
    }
}
