namespace ExamHelper.Core.Settings;
public interface ISecretStore
{
    Task<IDisposable?> AcquireReadLeaseAsync(CancellationToken ct) => Task.FromResult<IDisposable?>(null);
    Task SetAsync(string id, string secret, CancellationToken ct);
    Task<string?> GetAsync(string id, CancellationToken ct);
}