namespace ExamHelper.Core.Settings;
public interface ISecretStore
{
    Task SetAsync(string id, string secret, CancellationToken ct);
    Task<string?> GetAsync(string id, CancellationToken ct);
}