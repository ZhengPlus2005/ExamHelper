using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string,string> _secrets = new();
    public Task SetAsync(string id, string secret, CancellationToken ct) { _secrets[id] = secret; return Task.CompletedTask; }
    public Task<string?> GetAsync(string id, CancellationToken ct) => Task.FromResult(_secrets.GetValueOrDefault(id));
}