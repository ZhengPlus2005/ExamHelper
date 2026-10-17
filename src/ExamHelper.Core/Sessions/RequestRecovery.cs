using System.Collections.Immutable;
namespace ExamHelper.Core.Sessions;
public static class RequestRecovery
{
    public static SessionSnapshot RecoverOnStartup(SessionSnapshot session)
    {
        var active = session.PendingRequest ?? session.Requests.LastOrDefault(r => r.Status == MessageStatus.Pending);
        return session with
        {
            Generation = checked(session.Generation + 1),IsAuxiliaryMode = false,CaptureInProgress = false,SpeechUndoAvailable = false,
            AnswerState = AnswerState.Idle,SpeechState = SpeechState.Idle,
            PendingRequest = active is null ? null : active with { Status = MessageStatus.Failed },
            Requests = session.Requests.Select(r => r.Status == MessageStatus.Pending ? r with { Status = MessageStatus.Failed } : r).ToImmutableArray(),
            Messages = session.Messages.Select(m => m.Status == MessageStatus.Pending ? m with { Status = MessageStatus.Failed } : m).ToImmutableArray()
        };
    }
}