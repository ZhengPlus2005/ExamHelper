using System.IO;
using ExamHelper.Core.Drafts;
namespace ExamHelper.Windows.Capture;
public sealed class FileImageStore : IImageStore
{
    private readonly string _root;
    private readonly SemaphoreSlim _snapshotGate=new(1);
    private readonly ImageReferenceRegistry? _pending;
    public FileImageStore(string root,ImageReferenceRegistry? pending = null)
    {
        _pending = pending; _root = Path.GetFullPath(root); Directory.CreateDirectory(_root); CheckRoot();
    }
    public async Task<ImageRef> AddAsync(CapturedImage image, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); CheckRoot();
        if (image.Width <= 0 || image.Height <= 0 || image.PngBytes.Length == 0) throw new ArgumentException("图片为空或尺寸无效。", nameof(image));
        var id = Guid.NewGuid(); var name = id.ToString("N") + ".png";
        _pending?.Protect(id);
        var path = Path.Combine(_root, name); var temporary = Path.Combine(_root, id.ToString("N") + ".tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await stream.WriteAsync(image.PngBytes, ct); await stream.FlushAsync(ct);
            }
            ct.ThrowIfCancellationRequested(); File.Move(temporary, path);
            return new(id, name, image.Width, image.Height);
        }
        catch { _pending?.Release([id]); throw; }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public Task<byte[]> ReadAsync(ImageRef image, CancellationToken ct)
    {
        CheckRoot();
        if (!string.Equals(image.RelativePath, image.Id.ToString("N") + ".png", StringComparison.Ordinal))
            throw new ArgumentException("图片路径不在仓库允许范围内。", nameof(image));
        var path = Path.Combine(_root, image.RelativePath);
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("不读取图片符号链接。", nameof(image));
        return File.ReadAllBytesAsync(path, ct);
    }
    public async Task<IReadOnlyDictionary<Guid,byte[]>> ReadSnapshotAsync(IReadOnlyList<ImageRef> images,CancellationToken ct)
    {
        await _snapshotGate.WaitAsync(ct);
        try
        {
            var result=new Dictionary<Guid,byte[]>();
            foreach(var image in images.DistinctBy(i => i.Id)) result.Add(image.Id,await ReadAsync(image,ct));
            return result;
        }
        finally { _snapshotGate.Release(); }
    }
    public Task AbandonAsync(IReadOnlyList<ImageRef> images,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();_pending?.Release(images.Select(i=>i.Id));return Task.CompletedTask;
    }
    public async Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds, CancellationToken ct)
    {
        await _snapshotGate.WaitAsync(ct);
        try {
        CheckRoot();
        foreach (var path in Directory.EnumerateFiles(_root, "*.png", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            if (Guid.TryParseExact(name, "N", out var id) && !liveIds.Contains(id) && _pending?.Contains(id) != true) File.Delete(path);
        }
        }
        finally { _snapshotGate.Release(); }
    }
    private void CheckRoot()
    {
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("图片仓库不能是符号链接。");
    }
}