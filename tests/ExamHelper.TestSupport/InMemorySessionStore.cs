using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
namespace ExamHelper.TestSupport;
public sealed class InMemorySessionStore : ISessionStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid,SessionSnapshot> _sessions = new();
    public bool FailWrites { get; set; }
    public Task SaveAsync(SessionSnapshot snapshot,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(FailWrites) throw new ServiceFailure(FailureCategory.Storage,"模拟磁盘写入失败。");
        if(ConversationTitle.IsEmpty(snapshot)) _sessions.TryRemove(snapshot.Id,out _); else _sessions[snapshot.Id] = snapshot; return Task.CompletedTask;
    }
    public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct) => Task.FromResult(_sessions.GetValueOrDefault(id));
    public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionSummary>>(_sessions.Values.Select(s => new SessionSummary(s.Id,ConversationTitle.Get(s),s.UpdatedAt,s.Draft.HasContent) { CreatedAt=s.CreatedAt,SortAt=ConversationChronology.SortAt(s) }).OrderByDescending(s=>s.SortAt).ThenByDescending(s=>s.CreatedAt).ThenBy(s=>s.Id.ToString("D"),StringComparer.Ordinal).ToArray());
    public Task DeleteAsync(Guid id,CancellationToken ct) { _sessions.TryRemove(id,out _); return Task.CompletedTask; }
}