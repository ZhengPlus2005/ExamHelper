using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Audio;
public class SpeechCoordinatorTests
{
    [Fact]
    public async Task LateTranscriptAfterClearIsIgnored()
    {
        var mic = new Microphone(); await using var recording = new RecordingController(mic, new ManualTimeProvider());
        var asr = new DeferredAsr(); var session = Guid.NewGuid(); long generation = 0;
        await using var speech = new SpeechCoordinator(recording, asr, () => new AsrSettings(), () => (session,generation));
        var results = new List<SpeechTranscript>(); speech.TranscriptReady += results.Add;
        await speech.ToggleAsync(default); var stopping = speech.ToggleAsync(default); await asr.Started.Task;
        generation++; await speech.CancelAsync(); asr.Result.SetResult("过期");
        await stopping;
        Assert.Empty(results); Assert.Equal(SpeechState.Idle, speech.State); Assert.True(mic.CancelCalls > 0);
    }
    [Fact]
    public async Task FailedClipRetryKeepsSameAudioAndNeverRerecords()
    {
        var mic = new Microphone(); await using var recording = new RecordingController(mic, new ManualTimeProvider());
        var asr = new RetryAsr(); var session = Guid.NewGuid();
        await using var speech = new SpeechCoordinator(recording, asr, () => new AsrSettings(), () => (session,1L));
        SpeechTranscript? transcript = null; speech.TranscriptReady += value => transcript = value;
        await speech.ToggleAsync(default); await speech.ToggleAsync(default);
        Assert.Equal(SpeechState.RetryPending, speech.State); Assert.NotNull(speech.Error); Assert.Equal(0, mic.CancelCalls);
        await speech.RetryAsync(default);
        Assert.Equal(1, mic.StartCalls); Assert.Equal(asr.Clips[0], asr.Clips[1]);
        Assert.Equal("请使用 C++", transcript?.Transcript); Assert.Equal(session, transcript?.SessionId);
        Assert.Equal(SpeechState.Idle, speech.State); Assert.True(mic.CancelCalls > 0);
    }
    [Fact]
    public async Task SuccessDeletesTemporaryAudioAndAutomaticStopAlsoRecognizes()
    {
        var mic = new Microphone(); var clock = new ManualTimeProvider();
        await using var recording = new RecordingController(mic, clock);
        var asr = new RetryAsr { FailFirst = false };
        await using var speech = new SpeechCoordinator(recording, asr, () => new AsrSettings(), () => (Guid.Empty,2L));
        var completed = new TaskCompletionSource<SpeechTranscript>(TaskCreationOptions.RunContinuationsAsynchronously);
        speech.TranscriptReady += clip => completed.TrySetResult(clip);
        await speech.ToggleAsync(default); clock.Advance(TimeSpan.FromSeconds(60));
        var result = await completed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(2, result.Generation); Assert.Equal(1, mic.StopCalls); Assert.True(mic.CancelCalls > 0);
    }
    [Fact]
    public async Task QueuedToggleCannotRestartAfterLifecycleCancellation()
    {
        var mic = new GatedMicrophone(); await using var recording = new RecordingController(mic,new ManualTimeProvider());
        long generation = 1;
        await using var speech = new SpeechCoordinator(recording,new RetryAsr(),() => new AsrSettings(),() => (Guid.Empty,generation));
        var starting = speech.ToggleAsync(default); await mic.Started.Task;
        // Both calls are queued on the speech gate held by device startup.
        var cancelling = speech.CancelAsync();
        var staleToggle = speech.ToggleAsync(default);
        generation++;
        mic.Release.TrySetResult();
        await Task.WhenAll(starting,cancelling,staleToggle).WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(1,mic.StartCalls); Assert.Equal(SpeechState.Idle,speech.State);
        Assert.False(recording.IsRecording);
    }
    [Theory]
    [InlineData("toggle")]
    [InlineData("retry")]
    [InlineData("discard")]
    public async Task DispatchFromOldContextCannotAffectNewRecording(string command)
    {
        var mic = new Microphone(); await using var recording = new RecordingController(mic,new ManualTimeProvider());
        await using var speech = new SpeechCoordinator(recording,new RetryAsr(),() => new AsrSettings(),() => (Guid.Empty,2L));
        await speech.ToggleAsync(default);
        var oldContext = (Guid.Empty,1L);
        if(command == "toggle") await speech.ToggleAsync(default,oldContext);
        else if(command == "retry") await speech.RetryAsync(default,oldContext);
        else await speech.DiscardAsync(oldContext);
        Assert.Equal(SpeechState.Recording,speech.State);
        Assert.Equal(1,mic.StartCalls); Assert.Equal(0,mic.StopCalls); Assert.Equal(0,mic.CancelCalls);
    }
    private sealed class GatedMicrophone : IMicrophoneRecorder
    {
        public int StartCalls;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task StartAsync(string d,CancellationToken ct) { StartCalls++; Started.TrySetResult(); await Release.Task.WaitAsync(ct); }
        public Task<RecordedAudio> StopAsync(CancellationToken ct) => Task.FromResult(new RecordedAudio(Guid.NewGuid(),"test.wav",TimeSpan.FromSeconds(1)));
        public Task CancelAsync() => Task.CompletedTask;
    }
    private sealed class Microphone : IMicrophoneRecorder
    {
        public int StartCalls, StopCalls, CancelCalls;
        public Task StartAsync(string deviceId,CancellationToken ct) { StartCalls++; return Task.CompletedTask; }
        public Task<RecordedAudio> StopAsync(CancellationToken ct) { StopCalls++; return Task.FromResult(new RecordedAudio(Guid.NewGuid(),"retained.wav",TimeSpan.FromSeconds(1))); }
        public Task CancelAsync() { CancelCalls++; return Task.CompletedTask; }
    }
    private sealed class DeferredAsr : IAsrClient
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranscribeAsync(RecordedAudio a,AsrSettings s,CancellationToken ct) { Started.TrySetResult(); return Result.Task; }
    }
    private sealed class RetryAsr : IAsrClient
    {
        public bool FailFirst = true; public List<RecordedAudio> Clips { get; } = [];
        public Task<string> TranscribeAsync(RecordedAudio a,AsrSettings s,CancellationToken ct)
        {
            Clips.Add(a); return FailFirst && Clips.Count == 1 ? Task.FromException<string>(new ServiceFailure(FailureCategory.Transport,"连接失败")) : Task.FromResult("请使用 C++");
        }
    }
}