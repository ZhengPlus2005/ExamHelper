using System.Collections.Immutable;
using ExamHelper.Core.Drafts;
namespace ExamHelper.Core.Sessions;
public enum MessageStatus { Pending, Complete, Failed, Stopped }
public enum AnswerState { Idle, Generating }
public enum SpeechState { Idle, Recording, Recognizing, RetryPending }
public enum MessageRole { System, User, Assistant }
public sealed record MessageSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid RequestId { get; init; }
    public MessageRole Role { get; init; }
    public string Text { get; init; } = "";
    public ImmutableArray<ImageRef> Images { get; init; } = [];
    public MessageStatus Status { get; init; } = MessageStatus.Pending;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
public sealed record RequestSnapshot
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public Guid SessionId { get; init; }
    public long Generation { get; init; }
    public string Text { get; init; } = "";
    public ImmutableArray<ImageRef> Images { get; init; } = [];
    public string Prompt { get; init; } = "";
    public string SystemPrompt { get; init; } = "";
    public bool IsDirectCapture { get; init; }
    public MessageStatus Status { get; init; } = MessageStatus.Pending;
    public long? RestoredDraftRevision { get; init; }
}
public sealed record SessionSnapshot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public long Generation { get; init; }
    public ImmutableArray<MessageSnapshot> Messages { get; init; } = [];
    public DraftSnapshot Draft { get; init; } = DraftSnapshot.Empty;
    public RequestSnapshot? PendingRequest { get; init; }
    public SpeechState SpeechState { get; init; }
    public AnswerState AnswerState { get; init; }
    public bool IsAuxiliaryMode { get; init; } = true;
    public bool CaptureInProgress { get; init; }
    public bool SpeechUndoAvailable { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}
public sealed record SessionSummary(Guid Id, string Title, DateTimeOffset UpdatedAt, bool HasSavedDraft);