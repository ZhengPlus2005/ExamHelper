using System.IO;
using System.Windows.Media.Imaging;
using ExamHelper.Core.Drafts;
namespace ExamHelper.Windows.Images;
public interface IImageImporter
{
    Task<IReadOnlyList<ImageRef>> ImportFilesAsync(IEnumerable<string> paths,CancellationToken ct);
    Task<IReadOnlyList<ImageRef>> ImportBitmapAsync(BitmapSource bitmap,CancellationToken ct);
    Task AbandonAsync(IReadOnlyList<ImageRef> imported,CancellationToken ct) => Task.CompletedTask;
}
public sealed class ImageImportService(IImageStore images) : IImageImporter
{
    public Task AbandonAsync(IReadOnlyList<ImageRef> imported,CancellationToken ct) => images.AbandonAsync(imported,ct);
    public Task<IReadOnlyList<ImageRef>> ImportFilesAsync(IEnumerable<string> paths,CancellationToken ct)
    {
        var files = paths.ToArray();
        return Task.Run(async () =>
        {
            var result = new List<ImageRef>();
            try
            {
                foreach(var file in files)
                {
                    ct.ThrowIfCancellationRequested();
                    using var source = File.OpenRead(file);
                    var decoder = BitmapDecoder.Create(source,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad);
                    if(decoder.Frames.Count == 0) throw new ArgumentException("图片没有可读取的图像帧。");
                    result.Add(await SaveAsync(decoder.Frames[0],ct).ConfigureAwait(false));
                }
                return (IReadOnlyList<ImageRef>)result;
            }
            catch { await images.AbandonAsync(result,CancellationToken.None).ConfigureAwait(false);throw; }
        },ct);
    }
    public Task<IReadOnlyList<ImageRef>> ImportBitmapAsync(BitmapSource bitmap,CancellationToken ct)
    {
        var copy = bitmap.CloneCurrentValue(); copy.Freeze();
        return Task.Run(async () => (IReadOnlyList<ImageRef>)new[] { await SaveAsync(copy,ct).ConfigureAwait(false) },ct);
    }
    private async Task<ImageRef> SaveAsync(BitmapSource bitmap,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = new MemoryStream(); png.Save(output);
        return await images.AddAsync(new(output.ToArray(),bitmap.PixelWidth,bitmap.PixelHeight),ct).ConfigureAwait(false);
    }
}
