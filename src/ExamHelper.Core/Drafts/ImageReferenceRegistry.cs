using System.Collections.Concurrent;
namespace ExamHelper.Core.Drafts;
// New files remain protected through their first durable reference. Abandoned
// imports can be reclaimed on the next startup, when this in-memory set is empty.
public sealed class ImageReferenceRegistry
{
    private readonly ConcurrentDictionary<Guid,byte> _pending = new();
    public void Protect(Guid id) => _pending.TryAdd(id,0);
    public void Release(IEnumerable<Guid> ids) { foreach(var id in ids) _pending.TryRemove(id,out _); }
    public bool Contains(Guid id) => _pending.ContainsKey(id);
    public IReadOnlySet<Guid> Snapshot() => _pending.Keys.ToHashSet();
}
