using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
namespace ExamHelper.Windows.Persistence;
public sealed class ImageReferenceCleaner(ISessionStore sessions,IImageStore images,ImageReferenceRegistry? pending = null,Func<IReadOnlySet<Guid>>? inMemory = null)
{
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var live = new HashSet<Guid>();
        foreach(var summary in await sessions.ListAsync(ct).ConfigureAwait(false))
        {
            var session = await sessions.LoadAsync(summary.Id,ct).ConfigureAwait(false);
            if(session is not null) live.UnionWith(SqliteSessionStore.ImageReferences(session).Select(i => i.Id));
        }
        pending?.Release(live);
        if(inMemory is not null) live.UnionWith(inMemory());
        if(pending is not null) live.UnionWith(pending.Snapshot());
        await images.DeleteUnreferencedAsync(live,ct).ConfigureAwait(false);
    }
}
