using System.Collections.Immutable;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Ai;
public sealed record AiChunk(string Text, bool IsFinal, bool IsComplete = true,string? ReasoningContent = null);
public sealed record AiMessage(MessageRole Role, string Text, ImmutableArray<ImageRef> Images,string? ReasoningContent = null);
public sealed record AiRequest(Guid RequestId, ImmutableArray<AiMessage> Messages);
public interface IAiClient
{
    Task<IAiClient> PrepareAsync(AiSettings settings,CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult<IAiClient>(this); }
    IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request, AiSettings settings, CancellationToken ct);
}