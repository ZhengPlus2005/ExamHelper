using System.Collections.Immutable;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using Xunit;
namespace ExamHelper.Core.Tests.Sessions;

public class CommandPolicyTests
{
    private readonly CommandPolicy _policy = new();
    private static readonly DraftSnapshot Filled = new(" ", [], 1);
    [Fact]
    public void DirectSendRequiresEmptyDraftAndOrdinarySendRequiresContent()
    {
        var state = new SessionSnapshot();
        Assert.True(_policy.CanExecute(AppCommand.CaptureAndSend, state));
        Assert.False(_policy.CanExecute(AppCommand.Send, state));
        state = state with { Draft = Filled };
        Assert.False(_policy.CanExecute(AppCommand.CaptureAndSend, state));
        Assert.True(_policy.CanExecute(AppCommand.Send, state));
        Assert.NotEmpty(_policy.Evaluate(AppCommand.CaptureAndSend, state).Reason!);
    }
    [Theory]
    [InlineData(SpeechState.Recording)]
    [InlineData(SpeechState.Recognizing)]
    [InlineData(SpeechState.RetryPending)]
    public void SpeechWorkBlocksSendAndNewConversation(SpeechState speech)
    {
        var state = new SessionSnapshot { SpeechState = speech, Draft = Filled };
        Assert.False(_policy.CanExecute(AppCommand.Send, state));
        Assert.False(_policy.CanExecute(AppCommand.CaptureAndSend, state));
        Assert.False(_policy.CanExecute(AppCommand.NewConversation, state));
        Assert.True(_policy.CanExecute(AppCommand.ClearInput, state));
        Assert.True(_policy.CanExecute(AppCommand.CaptureToDraft, state));
        Assert.Equal(speech == SpeechState.Recording, _policy.CanExecute(AppCommand.ToggleRecording, state));
        Assert.Equal(speech == SpeechState.RetryPending, _policy.CanExecute(AppCommand.RetrySpeech, state));
    }
    [Fact]
    public void GeneratingCanPrepareNextDraftButCannotSendOrStartNew()
    {
        var state = new SessionSnapshot { AnswerState = AnswerState.Generating, Draft = Filled };
        Assert.True(_policy.CanExecute(AppCommand.CaptureToDraft, state));
        Assert.True(_policy.CanExecute(AppCommand.ToggleRecording, state));
        Assert.True(_policy.CanExecute(AppCommand.StopAnswer, state));
        foreach (var cmd in new[] { AppCommand.Send, AppCommand.CaptureAndSend, AppCommand.NewConversation })
            Assert.False(_policy.CanExecute(cmd, state));
    }
    [Fact]
    public void PendingRequestBlocksNewRequestsButAllowsPreparationAndRetry()
    {
        var state = new SessionSnapshot { Draft = Filled, PendingRequest = new RequestSnapshot() };
        foreach (var cmd in new[] { AppCommand.Send, AppCommand.CaptureAndSend, AppCommand.NewConversation })
            Assert.False(_policy.CanExecute(cmd, state));
        Assert.True(_policy.CanExecute(AppCommand.CaptureToDraft, state));
        Assert.True(_policy.CanExecute(AppCommand.ToggleRecording, state));
        Assert.True(_policy.CanExecute(AppCommand.RetryRequest, state));
        Assert.True(_policy.CanExecute(AppCommand.DiscardRequest, state));
    }
    [Fact]
    public void HiddenModeKeepsAvailabilityButLeavingAuxiliaryDisablesCommands()
    {
        var state = new SessionSnapshot();
        var before = Enum.GetValues<AppCommand>().ToDictionary(c => c, c => _policy.CanExecute(c, state));
        Assert.True(_policy.CanExecute(AppCommand.ToggleVisibility, state));
        Assert.All(before, item => Assert.Equal(item.Value, _policy.CanExecute(item.Key, state)));
        state = state with { IsAuxiliaryMode = false };
        Assert.All(Enum.GetValues<AppCommand>(), c => Assert.False(_policy.CanExecute(c, state)));
    }
    [Fact]
    public void CaptureCannotOverlapAndUndoRequiresLiveInsertion()
    {
        var state = new SessionSnapshot { CaptureInProgress = true };
        Assert.False(_policy.CanExecute(AppCommand.CaptureToDraft, state));
        Assert.False(_policy.CanExecute(AppCommand.CaptureAndSend, state));
        Assert.False(_policy.CanExecute(AppCommand.UndoSpeech, state));
        Assert.True(_policy.CanExecute(AppCommand.UndoSpeech, state with { SpeechUndoAvailable = true }));
    }
}