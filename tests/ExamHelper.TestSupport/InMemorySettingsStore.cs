using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public sealed class InMemorySettingsStore(AppSettings? initial = null) : ISettingsStore
{
    public AppSettings Value { get; private set; } = initial ?? new();
    public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
    public Task SaveAsync(AppSettings settings,CancellationToken ct) { Value = settings; return Task.CompletedTask; }
}