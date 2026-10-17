using System.IO;
using ExamHelper.Core.Drafts;
namespace ExamHelper.Windows.Capture;
public sealed class FileImageStore : IImageStore
{
    private readonly string _root;
    public FileImageStore(string root)
    {
        _root = Path.GetFullPath(root); Directory.CreateDirectory(_root); CheckRoot();
    }
    public async Task<ImageRef> AddAsync(CapturedImage image, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); CheckRoot();
        if (image.Width <= 0 || image.Height <= 0 || image.PngBytes.Length == 0) throw new ArgumentException("图片为空或尺寸无效。", nameof(image));
        var id = Guid.NewGuid(); var name = id.ToString("N") + ".png";
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
    public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds, CancellationToken ct)
    {
        CheckRoot();
        foreach (var path in Directory.EnumerateFiles(_root, "*.png", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            var name = Path.GetFileNameWithoutExtension(path);
            if (Guid.TryParseExact(name, "N", out var id) && !liveIds.Contains(id)) File.Delete(path);
        }
        return Task.CompletedTask;
    }
    private void CheckRoot()
    {
        if ((File.GetAttributes(_root) & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("图片仓库不能是符号链接。");
    }
}