using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
namespace ExamHelper.Windows.Persistence;
// Run before opening an overlay, or after a confirmed deletion while at home.
// Never run concurrently with a capture awaiting its first database reference.
public sealed class ImageReferenceCleaner(ISessionStore sessions,IImageStore images)
{
    public async Task ReconcileAsync(CancellationToken ct)
    {
        var live = new HashSet<Guid>();
        foreach(var summary in await sessions.ListAsync(ct).ConfigureAwait(false))
        {
            var session = await sessions.LoadAsync(summary.Id,ct).ConfigureAwait(false);
            if(session is not null) live.UnionWith(SqliteSessionStore.ImageReferences(session).Select(i => i.Id));
        }
        await images.DeleteUnreferencedAsync(live,ct).ConfigureAwait(false);
    }
}