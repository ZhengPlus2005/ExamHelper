using ExamHelper.Core.Interaction;
namespace ExamHelper.Core.Sessions;
public sealed record CommandAvailability(bool Allowed, string? Reason = null);
public sealed class CommandPolicy
{
    public bool CanExecute(AppCommand command, SessionSnapshot state) => Evaluate(command, state).Allowed;
    public CommandAvailability Evaluate(AppCommand command, SessionSnapshot state)
    {
        if (!state.IsAuxiliaryMode) return new(false, "请先进入辅助模式。");
        bool generating = state.AnswerState == AnswerState.Generating;
        bool speech = state.SpeechState != SpeechState.Idle;
        bool pending = state.PendingRequest is not null;
        bool draft = state.Draft.HasContent;
        return command switch
        {
            AppCommand.CaptureAndSend => Check(!draft && !speech && !generating && !pending && !state.CaptureInProgress,
                "请先发送或清空草稿，并等待当前任务结束。"),
            AppCommand.CaptureToDraft => Check(!state.CaptureInProgress, "正在截图，请稍候。"),
            AppCommand.Send => Check(draft && !speech && !generating && !pending && !state.CaptureInProgress,
                "需要非空草稿，且录音、识别和当前请求已处理完毕。"),
            AppCommand.NewConversation => Check(!draft && !speech && !generating && !pending && !state.CaptureInProgress,
                "请先处理草稿和当前任务。"),
            AppCommand.ToggleRecording => Check(state.SpeechState is SpeechState.Idle or SpeechState.Recording,
                "请先完成识别或处理待重试录音。"),
            AppCommand.StopAnswer => Check(generating, "当前没有正在生成的回答。"),
            AppCommand.UndoSpeech => Check(state.SpeechUndoAvailable, "语音撤销已过期。"),
            AppCommand.RetrySpeech or AppCommand.DiscardSpeech => Check(state.SpeechState == SpeechState.RetryPending, "当前没有待处理录音。"),
            AppCommand.RetryRequest or AppCommand.DiscardRequest => Check(pending && !generating && !speech, "当前没有可处理的失败请求，或正在录音／识别。"),
            AppCommand.ClearInput or AppCommand.ToggleVisibility or AppCommand.ToggleCollapse or AppCommand.ReturnHome or AppCommand.Exit => new(true),
            _ => new(false, "未知操作。")
        };
    }
    private static CommandAvailability Check(bool allowed, string reason) => new(allowed, allowed ? null : reason);
}