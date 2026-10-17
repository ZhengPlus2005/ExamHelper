namespace ExamHelper.Core.Sessions;
public interface ISessionStore
{
    Task SaveAsync(SessionSnapshot snapshot,CancellationToken ct);
    Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct);
    Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct);
    Task DeleteAsync(Guid id,CancellationToken ct);
}