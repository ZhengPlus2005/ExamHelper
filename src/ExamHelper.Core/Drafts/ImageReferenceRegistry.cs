namespace ExamHelper.Core.Drafts;
// A sweep retains imports through its complete read/delete interval, even when
// another conversation commits and releases the import's first-save protection.
public sealed class ImageReferenceRegistry
{
    private readonly object _sync=new();
    private readonly HashSet<Guid> _pending=[];
    private readonly List<HashSet<Guid>> _sweeps=[];
    public void Protect(Guid id) { lock(_sync) { _pending.Add(id);foreach(var sweep in _sweeps) sweep.Add(id); } }
    public void Release(IEnumerable<Guid> ids) { lock(_sync) foreach(var id in ids) _pending.Remove(id); }
    public bool Contains(Guid id) { lock(_sync) return _pending.Contains(id) || _sweeps.Any(s => s.Contains(id)); }
    public IReadOnlySet<Guid> Snapshot() { lock(_sync) return _pending.Concat(_sweeps.SelectMany(s => s)).ToHashSet(); }
    public IDisposable BeginReconciliation()
    {
        lock(_sync) { var retained=_pending.ToHashSet();_sweeps.Add(retained);return new SweepLease(this,retained); }
    }
    private sealed class SweepLease(ImageReferenceRegistry registry,HashSet<Guid> retained) : IDisposable
    {
        private ImageReferenceRegistry? _registry=registry;
        public void Dispose() { var owner=Interlocked.Exchange(ref _registry,null);if(owner is not null) lock(owner._sync) owner._sweeps.Remove(retained); }
    }
}
