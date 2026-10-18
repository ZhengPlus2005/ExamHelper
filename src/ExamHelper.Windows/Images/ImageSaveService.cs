using ExamHelper.Windows.Common;
namespace ExamHelper.Windows.Images;
public sealed class ImageSaveService
{
    public Task SaveAsync(string path,ReadOnlyMemory<byte> originalPng,CancellationToken ct)=>AtomicFile.WriteAsync(path,originalPng,ct);
}
