using ExamHelper.Core.Audio;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Audio;
public class RecordingControllerTests
{
    [Fact]
    public async Task StopsOnceAt60Seconds()
    {
        var clock = new ManualTimeProvider(); var mic = new Microphone();
        await using var recording = new RecordingController(mic, clock);
        RecordedAudio? completed = null; recording.Stopped += clip => completed = clip;
        await recording.StartAsync("", default);
        clock.Advance(TimeSpan.FromMilliseconds(59999));
        Assert.True(recording.IsRecording); Assert.Equal(0, mic.StopCalls);
        clock.Advance(TimeSpan.FromMilliseconds(1));
        Assert.False(recording.IsRecording); Assert.Equal(1, mic.StopCalls);
        Assert.NotNull(completed); Assert.Equal(TimeSpan.FromSeconds(60), recording.Elapsed);
        clock.Advance(TimeSpan.FromSeconds(10)); Assert.Equal(1, mic.StopCalls);
    }
    [Fact]
    public async Task ManualStopRacingTimeoutProducesOneClip()
    {
        var clock = new ManualTimeProvider(); var mic = new Microphone { DelayStop = true };
        await using var recording = new RecordingController(mic, clock);
        int events = 0; recording.Stopped += _ => events++;
        await recording.StartAsync("", default);
        var manual = recording.StopAsync(default);
        clock.Advance(TimeSpan.FromSeconds(60));
        var duplicate = recording.StopAsync(default);
        Assert.Equal(1, mic.StopCalls);
        mic.CompleteStop(); var a = await manual; var b = await duplicate;
        Assert.Equal(a.OperationId, b.OperationId); Assert.Equal(1, events);
    }
    [Fact]
    public async Task CancelDeletesAudioAndPreventsTimeoutCallback()
    {
        var clock = new ManualTimeProvider(); var mic = new Microphone();
        await using var recording = new RecordingController(mic, clock);
        await recording.StartAsync("", default); await recording.CancelAsync();
        clock.Advance(TimeSpan.FromSeconds(60));
        Assert.False(recording.IsRecording); Assert.Equal(1, mic.CancelCalls); Assert.Equal(0, mic.StopCalls);
    }
    private sealed class Microphone : IMicrophoneRecorder
    {
        public int StopCalls, CancelCalls; public bool DelayStop;
        private readonly TaskCompletionSource<RecordedAudio> _stop = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly RecordedAudio _clip = new(Guid.NewGuid(), "temporary.wav", TimeSpan.FromSeconds(60));
        public Task StartAsync(string deviceId, CancellationToken ct) => Task.CompletedTask;
        public Task<RecordedAudio> StopAsync(CancellationToken ct) { StopCalls++; return DelayStop ? _stop.Task : Task.FromResult(_clip); }
        public void CompleteStop() => _stop.TrySetResult(_clip);
        public Task CancelAsync() { CancelCalls++; return Task.CompletedTask; }
    }
}