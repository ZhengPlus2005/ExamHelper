using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;
public class SessionCoordinatorTests
{
    [Fact]
    public async Task FailureRestoresEmptyDraft()
    {
        await using var rig = new Rig(); await rig.Start("原始问题");
        var attempt = rig.Ai.QueueAttempt(); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        Assert.False(rig.Coordinator.State.Draft.HasContent);
        attempt.Text("片段"); attempt.Fail(new ServiceFailure(FailureCategory.Transport,"失败")); await rig.Coordinator.WaitForIdleAsync(default);
        Assert.Equal("原始问题",rig.Coordinator.State.Draft.Text); Assert.NotNull(rig.Coordinator.State.PendingRequest);
        Assert.Equal("片段",rig.Coordinator.State.Messages[^1].Text); Assert.Equal(MessageStatus.Failed,rig.Coordinator.State.Messages[^1].Status);
    }
    [Fact]
    public async Task FailureKeepsNewDraftAndPendingRequest()
    {
        await using var rig = new Rig(); await rig.Start("旧问题");
        var attempt = rig.Ai.QueueAttempt(); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        await rig.Voice("下一题");
        attempt.Fail(new ServiceFailure(FailureCategory.Transport,"失败")); await rig.Coordinator.WaitForIdleAsync(default);
        Assert.Equal("下一题",rig.Coordinator.State.Draft.Text); Assert.Equal("旧问题",rig.Coordinator.State.PendingRequest!.Text);
        await Assert.ThrowsAsync<InvalidOperationException>(() => rig.Coordinator.ExecuteAsync(AppCommand.Send,default));
    }
    [Fact]
    public async Task RetryTwiceKeepsOneUserMessageAndReplacesAnswer()
    {
        await using var rig = new Rig(); await rig.Start("问题");
        for(int i = 0;i < 3;i++)
        {
            var attempt = rig.Ai.QueueAttempt();
            await rig.Coordinator.ExecuteAsync(i == 0 ? AppCommand.Send : AppCommand.RetryRequest,default);
            var request = await attempt.Started.Task;
            Assert.Single(request.Messages.Where(m => m.Role == MessageRole.User));
            attempt.Text(i == 2 ? "最终答案" : "失败片段");
            if(i < 2) attempt.Fail(new ServiceFailure(FailureCategory.Transport,"失败")); else attempt.Complete();
            await rig.Coordinator.WaitForIdleAsync(default);
        }
        Assert.Single(rig.Coordinator.State.Messages.Where(m => m.Role == MessageRole.User));
        Assert.Equal("最终答案",rig.Coordinator.State.Messages[^1].Text); Assert.Null(rig.Coordinator.State.PendingRequest);
        Assert.False(rig.Coordinator.State.Draft.HasContent);
    }
    [Fact]
    public async Task StoppedLateChunkAfterNewSessionIsIgnored()
    {
        await using var rig = new Rig(); await rig.Start("旧问题");
        var attempt = rig.Ai.QueueAttempt(true); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        var waiting = rig.Coordinator.WaitForIdleAsync(default);
        await rig.Coordinator.ReturnHomeAsync(default); await rig.Coordinator.ExecuteAsync(AppCommand.StopAnswer,default); await rig.Coordinator.StartNewAsync(default); var id = rig.Coordinator.State.Id;
        attempt.Text("晚到旧答案"); attempt.Complete(); await waiting;
        Assert.Equal(id,rig.Coordinator.State.Id); Assert.Empty(rig.Coordinator.State.Messages);
    }
    [Fact]
    public async Task ReturnAndExitBothPreserveDraft()
    {
        await using var rig = new Rig(); await rig.Start("返回时清除"); var first = rig.Coordinator.State.Id;
        await rig.Coordinator.ReturnHomeAsync(default);
        Assert.Equal("返回时清除",(await rig.Store.LoadAsync(first,default))!.Draft.Text);
        await rig.Coordinator.StartNewAsync(default); await rig.Voice("退出时保留"); var second = rig.Coordinator.State.Id;
        await rig.Coordinator.ExitAsync(default);
        Assert.Equal("退出时保留",(await rig.Store.LoadAsync(second,default))!.Draft.Text);
        await rig.Coordinator.ContinueAsync(second,default); Assert.Equal("退出时保留",rig.Coordinator.State.Draft.Text);
        Assert.False(rig.Coordinator.State.SpeechUndoAvailable);
    }
    [Fact]
    public async Task ClearCancelsSpeechAndDiscardsLateResultButKeepsCurrentAnswer()
    {
        await using var rig = new Rig(); await rig.Start("问题"); var oldGeneration = rig.Coordinator.State.SpeechGeneration;
        var attempt = rig.Ai.QueueAttempt(); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default); await attempt.Started.Task;
        await rig.Coordinator.ExecuteAsync(AppCommand.ClearInput,default);
        await rig.Coordinator.AppendSpeechResultAsync(new(rig.Coordinator.State.Id,oldGeneration,Guid.NewGuid(),"过期文字"),default);
        Assert.False(rig.Coordinator.State.Draft.HasContent); Assert.Equal(AnswerState.Generating,rig.Coordinator.State.AnswerState);
        attempt.Text("仍然完成的答案"); attempt.Complete(); await rig.Coordinator.WaitForIdleAsync(default);
        Assert.Equal("仍然完成的答案",rig.Coordinator.State.Messages[^1].Text);
        Assert.Equal(MessageStatus.Complete,rig.Coordinator.State.Messages[^1].Status);
    }
    [Fact]
    public async Task SaveFailureKeepsRecoverableInMemoryInputAndDoesNotStartNetwork()
    {
        await using var rig = new Rig(); await rig.Start("不能丢掉");
        rig.Store.FailWrites = true; var attempt = rig.Ai.QueueAttempt();
        await Assert.ThrowsAsync<ServiceFailure>(() => rig.Coordinator.ExecuteAsync(AppCommand.Send,default));
        Assert.False(attempt.Started.Task.IsCompleted); Assert.Equal("不能丢掉",rig.Coordinator.State.Draft.Text);
        Assert.NotNull(rig.Coordinator.Error); rig.Store.FailWrites = false;
    }
    [Fact]
    public async Task FailedReturnKeepsPendingIdentityAndFailedContinueKeepsHomeMode()
    {
        await using var rig = new Rig(); await rig.Start("必须保留");
        var attempt = rig.Ai.QueueAttempt(); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default);
        await attempt.Started.Task; attempt.Fail(new ServiceFailure(FailureCategory.Transport,"失败"));
        await rig.Coordinator.WaitForIdleAsync(default);
        var pending = rig.Coordinator.State.PendingRequest!.RequestId;
        rig.Store.FailWrites = true;
        await Assert.ThrowsAsync<ServiceFailure>(() => rig.Coordinator.ReturnHomeAsync(default));
        Assert.Equal(pending,rig.Coordinator.State.PendingRequest?.RequestId);
        Assert.Equal("必须保留",rig.Coordinator.State.Draft.Text);
        Assert.True(rig.Coordinator.State.IsAuxiliaryMode);
        rig.Store.FailWrites = false;
        await rig.Coordinator.ExitAsync(default); var id = rig.Coordinator.State.Id;
        rig.Store.FailWrites = true;
        await Assert.ThrowsAsync<ServiceFailure>(() => rig.Coordinator.ContinueAsync(id,default));
        Assert.False(rig.Coordinator.State.IsAuxiliaryMode);
        rig.Store.FailWrites = false;
    }
    [Fact]
    public async Task MicrophoneRecognitionIsAppendedAndUndoKeepsImages()
    {
        await using var rig = new Rig(); await rig.Coordinator.StartNewAsync(default);
        await rig.Coordinator.ExecuteAsync(AppCommand.CaptureToDraft,default);
        await rig.Coordinator.ExecuteAsync(AppCommand.ToggleRecording,default);
        Assert.Equal(SpeechState.Recording,rig.Coordinator.State.SpeechState);
        await rig.Coordinator.ExecuteAsync(AppCommand.ToggleRecording,default);
        Assert.Equal("识别文字",rig.Coordinator.State.Draft.Text);
        Assert.True(rig.Coordinator.State.SpeechUndoAvailable);
        await rig.Coordinator.ExecuteAsync(AppCommand.UndoSpeech,default);
        Assert.Equal("",rig.Coordinator.State.Draft.Text); Assert.Single(rig.Coordinator.State.Draft.Images);
    }
    [Fact]
    public async Task ClearAfterDirectCaptureKeepsAnswerRunning()
    {
        await using var rig = new Rig(); await rig.Coordinator.StartNewAsync(default);
        var attempt = rig.Ai.QueueAttempt();
        await rig.Coordinator.ExecuteAsync(AppCommand.CaptureAndSend,default); await attempt.Started.Task;
        await rig.Voice("下一题草稿");
        await rig.Coordinator.ExecuteAsync(AppCommand.ClearInput,default);
        attempt.Text("直发完整答案"); attempt.Complete(); await rig.Coordinator.WaitForIdleAsync(default);
        Assert.Equal(MessageStatus.Complete,rig.Coordinator.State.Messages[^1].Status);
        Assert.Equal("直发完整答案",rig.Coordinator.State.Messages[^1].Text);
        Assert.False(rig.Coordinator.State.Draft.HasContent);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredFailedRequestClearsOnlyItsUnmodifiedDraft(bool retry)
    {
        await using var rig = new Rig();
        var request = new RequestSnapshot { Text = "失败原题",Status = MessageStatus.Failed,RestoredDraftRevision = 42 };
        var saved = new SessionSnapshot { IsAuxiliaryMode = false,Draft = new("失败原题",[],42),PendingRequest = request,Requests = [request] };
        // Match disk serialization rather than sharing the original snapshot instance.
        saved = System.Text.Json.JsonSerializer.Deserialize<SessionSnapshot>(System.Text.Json.JsonSerializer.Serialize(saved))!;
        await rig.Store.SaveAsync(saved,default); await rig.Coordinator.ContinueAsync(saved.Id,default);
        if(retry)
        {
            var attempt = rig.Ai.QueueAttempt();
            await rig.Coordinator.ExecuteAsync(AppCommand.RetryRequest,default); await attempt.Started.Task;
            attempt.Complete(); await rig.Coordinator.WaitForIdleAsync(default);
        }
        else await rig.Coordinator.ExecuteAsync(AppCommand.DiscardRequest,default);
        Assert.False(rig.Coordinator.State.Draft.HasContent);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestoredModifiedDraftCannotCollideWithAnotherBufferRevision(bool retry)
    {
        await using var rig = new Rig();
        var request = new RequestSnapshot { Text = "原题",Status = MessageStatus.Failed,RestoredDraftRevision = 1 };
        var saved = new SessionSnapshot { IsAuxiliaryMode = false,Draft = new("原题与后来追加",[],42),PendingRequest = request,Requests = [request] };
        await rig.Store.SaveAsync(saved,default); await rig.Coordinator.ContinueAsync(saved.Id,default);
        if(retry)
        {
            var attempt = rig.Ai.QueueAttempt();
            await rig.Coordinator.ExecuteAsync(AppCommand.RetryRequest,default); await attempt.Started.Task;
            attempt.Complete(); await rig.Coordinator.WaitForIdleAsync(default);
        }
        else await rig.Coordinator.ExecuteAsync(AppCommand.DiscardRequest,default);
        Assert.Equal("原题与后来追加",rig.Coordinator.State.Draft.Text);
    }
    [Fact]
    public async Task FailedReturnRollbackPreservesFailedDraftIdentity()
    {
        await using var rig = new Rig(); await rig.Start("原题");
        var attempt = rig.Ai.QueueAttempt(); await rig.Coordinator.ExecuteAsync(AppCommand.Send,default);
        await attempt.Started.Task; attempt.Fail(new ServiceFailure(FailureCategory.Transport,"失败"));
        await rig.Coordinator.WaitForIdleAsync(default); rig.Store.FailWrites = true;
        await Assert.ThrowsAsync<ServiceFailure>(() => rig.Coordinator.ReturnHomeAsync(default));
        Assert.Equal(rig.Coordinator.State.Draft.Revision,rig.Coordinator.State.PendingRequest!.RestoredDraftRevision);
        rig.Store.FailWrites = false;
        await rig.Coordinator.ExecuteAsync(AppCommand.DiscardRequest,default);
        Assert.False(rig.Coordinator.State.Draft.HasContent);
    }
    private sealed class Rig : IAsyncDisposable
    {
        public DeferredAiClient Ai { get; } = new(); public InMemorySessionStore Store { get; } = new();
        public SessionCoordinator Coordinator { get; }
        private readonly AppSettings _settings;
        public Rig()
        {
            var proof = new ServiceVerification(new byte[32]); _settings = TestSettings.Qualified(proof);
            Coordinator = new(Ai,new Asr(),new Microphone(),new Screen(),new Images(),() => _settings,Store,new ManualTimeProvider(),verification:proof);
        }
        public async Task Start(string text) { await Coordinator.StartNewAsync(default); await Voice(text); }
        public Task Voice(string text) => Coordinator.AppendSpeechResultAsync(new(Coordinator.State.Id,Coordinator.State.SpeechGeneration,Guid.NewGuid(),text),default);
        public ValueTask DisposeAsync() => Coordinator.DisposeAsync();
    }
    private sealed class Asr : IAsrClient { public Task<string> TranscribeAsync(RecordedAudio a,AsrSettings s,CancellationToken ct) => Task.FromResult("识别文字"); }
    private sealed class Microphone : IMicrophoneRecorder
    {
        public Task StartAsync(string d,CancellationToken ct) => Task.CompletedTask;
        public Task<RecordedAudio> StopAsync(CancellationToken ct) => Task.FromResult(new RecordedAudio(Guid.NewGuid(),"temp.wav",TimeSpan.FromSeconds(1)));
        public Task CancelAsync() => Task.CompletedTask;
    }
    private sealed class Screen : IScreenCapture { public Task<CapturedImage> CaptureAsync(CancellationToken ct) => Task.FromResult(new CapturedImage([1],1920,1080)); }
    private sealed class Images : IImageStore
    {
        public Task<ImageRef> AddAsync(CapturedImage i,CancellationToken ct) => Task.FromResult(new ImageRef(Guid.NewGuid(),"test.png",i.Width,i.Height));
        public Task<byte[]> ReadAsync(ImageRef i,CancellationToken ct) => Task.FromResult<byte[]>([1]);
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct) => Task.CompletedTask;
    }
}