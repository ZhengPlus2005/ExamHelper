namespace ExamHelper.Core.Drafts;
public interface IImageStore
{
    Task<ImageRef> AddAsync(CapturedImage image, CancellationToken ct);
    Task<byte[]> ReadAsync(ImageRef image, CancellationToken ct);
    async Task<IReadOnlyDictionary<Guid,byte[]>> ReadSnapshotAsync(IReadOnlyList<ImageRef> images,CancellationToken ct)
    {
        var result=new Dictionary<Guid,byte[]>();
        foreach(var image in images.DistinctBy(i => i.Id)) result.Add(image.Id,await ReadAsync(image,ct));
        return result;
    }
    Task AbandonAsync(IReadOnlyList<ImageRef> images,CancellationToken ct) => Task.CompletedTask;
    Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds, CancellationToken ct);
}