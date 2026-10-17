using System.IO;
using ExamHelper.Core.Drafts;
using ExamHelper.Windows.Capture;
using Xunit;
namespace ExamHelper.Windows.Tests.Capture;
public class FileImageStoreTests
{
    [Fact]
    public async Task StoresOriginalBytesAndOrderIndependentIds()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExamHelper-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileImageStore(root);
            byte[] bytes = [137,80,78,71,13,10,26,10,1,2,3];
            var a = await store.AddAsync(new(bytes, 1920, 1080), default);
            var b = await store.AddAsync(new(bytes, 1920, 1080), default);
            Assert.NotEqual(a.Id, b.Id); Assert.Equal(1920, a.Width);
            Assert.Equal(bytes, await store.ReadAsync(a, default));
            await store.DeleteUnreferencedAsync(new HashSet<Guid> { b.Id }, default);
            Assert.False(File.Exists(Path.Combine(root, a.RelativePath)));
            Assert.Equal(bytes, await store.ReadAsync(b, default));
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Theory]
    [InlineData("../outside.png")]
    [InlineData("nested/test.png")]
    [InlineData("C:\\outside.png")]
    [InlineData("wrong.png")]
    public async Task RejectsPathEscape(string path)
    {
        var root = Path.Combine(Path.GetTempPath(), "ExamHelper-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileImageStore(root);
            await Assert.ThrowsAsync<ArgumentException>(() => store.ReadAsync(new(Guid.NewGuid(), path, 1, 1), default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    [Fact]
    public async Task CancelledWriteDoesNotLeavePartialImage()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExamHelper-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new FileImageStore(root);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.AddAsync(new([1,2], 1, 1), cancel.Token));
            Assert.Empty(Directory.GetFiles(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}