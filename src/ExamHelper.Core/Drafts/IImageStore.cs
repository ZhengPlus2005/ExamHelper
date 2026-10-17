namespace ExamHelper.Core.Drafts;
public interface IImageStore
{
    Task<ImageRef> AddAsync(CapturedImage image, CancellationToken ct);
    Task<byte[]> ReadAsync(ImageRef image, CancellationToken ct);
    Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds, CancellationToken ct);
}