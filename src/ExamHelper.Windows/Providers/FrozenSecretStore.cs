using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Providers;
public sealed class FrozenSecretStore(string id,string key) : ISecretStore
{
    public Task<string?> GetAsync(string requested,CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult<string?>(requested == id ? key : null); }
    public Task SetAsync(string id,string value,CancellationToken ct) => throw new NotSupportedException("请求凭据快照只读。");
}
