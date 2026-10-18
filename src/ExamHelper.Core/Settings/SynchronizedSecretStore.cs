namespace ExamHelper.Core.Settings;
// Settings retirement and request preparation share this short-lived lease.
// Hold it before taking the settings snapshot; release once the key is copied.
public sealed class SynchronizedSecretStore(ISecretStore inner) : ISecretStore
{
    private readonly SemaphoreSlim _gate=new(1);
    public Task<string?> GetAsync(string id,CancellationToken ct)=>inner.GetAsync(id,ct);
    public async Task SetAsync(string id,string secret,CancellationToken ct)
    { await _gate.WaitAsync(ct).ConfigureAwait(false);try { await inner.SetAsync(id,secret,ct).ConfigureAwait(false); }finally { _gate.Release(); } }
    public async Task<IDisposable?> AcquireReadLeaseAsync(CancellationToken ct)
    { await _gate.WaitAsync(ct).ConfigureAwait(false);return new Lease(_gate); }
    private sealed class Lease(SemaphoreSlim gate) : IDisposable
    { private SemaphoreSlim? _gate=gate;public void Dispose()=>Interlocked.Exchange(ref _gate,null)?.Release(); }
}
