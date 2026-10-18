using ExamHelper.Core.Audio;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public class BackgroundConversationTests
{
    [Fact] public async Task ReturnPreservesLiveAnswerAndDraftWhileAnotherConversationIsSelected()
    {
        await using var rig = new ConversationWorkspaceRig(); var a = await rig.Workspace.NewAsync(true,default);
        await a.UpdateDraftTextAsync("question",default); var answer = rig.Ai.QueueAttempt();
        await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task; await a.UpdateDraftTextAsync("next",default);
        await rig.Workspace.ReturnHomeAsync(default); Assert.False(a.State.IsAuxiliaryMode);
        var b = await rig.Workspace.NewAsync(false,default); answer.Text("received"); answer.Complete(); await a.WaitForIdleAsync(default);
        Assert.Equal("received",a.State.Messages[^1].Text); Assert.Equal("next",a.State.Draft.Text); Assert.Empty(b.State.Messages);
    }
    [Fact] public async Task ReturnStopsRecordingWithoutWaitingForRecognitionAndTranscriptTargetsOriginal()
    {
        await using var rig = new ConversationWorkspaceRig(); var asr = new PendingAsr(); rig.Asr = asr;
        var a = await rig.Workspace.NewAsync(true,default); await a.ExecuteAsync(AppCommand.ToggleRecording,default);
        await rig.Workspace.ReturnHomeAsync(default).WaitAsync(TimeSpan.FromSeconds(2)); await asr.Entered.Task;
        var b = await rig.Workspace.NewAsync(false,default); await b.UpdateDraftTextAsync("B",default);
        asr.Done.TrySetResult("A voice");
        await WaitUntil(() => a.State.Draft.Text == "A voice");
        Assert.Equal("B",b.State.Draft.Text); Assert.False(a.State.IsAuxiliaryMode);
    }
    [Fact] public async Task ModelSwitchAffectsNextRequestAndNeverChangesPersistedDefaults()
    {
        await using var rig = new ConversationWorkspaceRig(); var first = new AiModelProfile { Name = "kimi-k3" }; var second = new AiModelProfile { Name = "other" };
        var profile = new AiServiceProfile { Name = "测试配置",BaseAddress = "https://example.invalid/v1",Settings = new() { SecretFingerprint = "test-digest" },Models = [first,second],DefaultModelId = first.Id };
        rig.Settings = AiProfileCatalog.Normalize(new() { AiProfiles = [profile],ActiveAiProfileId = profile.Id });
        var a = await rig.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("q",default);
        var answer = rig.Ai.QueueAttempt(); await a.ExecuteAsync(AppCommand.Send,default); await answer.Started.Task;
        await a.SelectModelAsync(ModelCatalog.Select(profile,second),default);
        answer.Complete(); await a.WaitForIdleAsync(default);
        Assert.Equal(second.Id,a.State.Selection!.ModelId); Assert.Equal(first.Id,rig.Settings.AiProfiles!.Value[0].DefaultModelId);
        Assert.Equal("kimi-k3",answer.SettingsUsed!.Model);
        await a.UpdateDraftTextAsync("next",default); var next = rig.Ai.QueueAttempt();
        await a.ExecuteAsync(AppCommand.Send,default); await next.Started.Task; Assert.Equal("other",next.SettingsUsed!.Model);
        next.Complete(); await a.WaitForIdleAsync(default);
    }
    [Fact] public async Task SeparateMicrophoneOwnersDoNotDeleteEachOthersStoppedClips()
    {
        var gate = new AiGenerationGate(); var innerA = new Recorder(); var innerB = new Recorder();
        var a = new ExclusiveMicrophoneRecorder(innerA,gate); var b = new ExclusiveMicrophoneRecorder(innerB,gate);
        await a.StartAsync("",default); await Assert.ThrowsAsync<InvalidOperationException>(() => b.StartAsync("",default));
        await a.StopAsync(default); await b.StartAsync("",default); await a.CancelAsync();
        Assert.True(innerB.Recording); Assert.True(gate.IsBusy); await b.CancelAsync(); Assert.False(gate.IsBusy);
    }
    private sealed class Recorder : IMicrophoneRecorder
    {
        public bool Recording;
        public Task StartAsync(string id,CancellationToken ct) { Recording = true; return Task.CompletedTask; }
        public Task<RecordedAudio> StopAsync(CancellationToken ct) { Recording = false; return Task.FromResult(new RecordedAudio(Guid.NewGuid(),"owned.wav",TimeSpan.FromSeconds(1))); }
        public Task CancelAsync() { Recording = false; return Task.CompletedTask; }
    }
    private static async Task WaitUntil(Func<bool> condition) { using var limit = new CancellationTokenSource(TimeSpan.FromSeconds(3)); while(!condition()) await Task.Delay(5,limit.Token); }
    private sealed class PendingAsr : IAsrClient
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Done { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranscribeAsync(RecordedAudio audio,AsrSettings settings,CancellationToken ct) { Entered.TrySetResult(); return Done.Task.WaitAsync(ct); }
    }
}
