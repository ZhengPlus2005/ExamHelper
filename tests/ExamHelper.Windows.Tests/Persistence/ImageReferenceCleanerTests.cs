using System.IO;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Persistence;
using Xunit;
namespace ExamHelper.Windows.Tests.Persistence;
public class ImageReferenceCleanerTests
{
    [Fact]
    public async Task DeleteConversationKeepsOtherReferencesAndOrphansAreCollected()
    {
        var root = NewRoot();
        try
        {
            var store = new SqliteSessionStore(Path.Combine(root,"history.db")); var images = new FileImageStore(Path.Combine(root,"Images"));
            var shared = await images.AddAsync(new([1,2,3],100,100),default);
            var orphan = await images.AddAsync(new([4,5],1,1),default);
            var first = new SessionSnapshot { Draft = new("",[shared],1) };
            var second = new SessionSnapshot { Messages = [new() { Role = MessageRole.User,Images = [shared],Status = MessageStatus.Complete }] };
            await store.SaveAsync(first,default); await store.SaveAsync(second,default);
            await store.DeleteAsync(first.Id,default);
            await new ImageReferenceCleaner(store,images).ReconcileAsync(default);
            Assert.Equal(new byte[] {1,2,3},await images.ReadAsync(shared,default));
            Assert.False(File.Exists(Path.Combine(root,"Images",orphan.RelativePath)));
            await store.DeleteAsync(second.Id,default); await new ImageReferenceCleaner(store,images).ReconcileAsync(default);
            Assert.False(File.Exists(Path.Combine(root,"Images",shared.RelativePath)));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task InterruptedImageDeleteCanBeRetriedAndRecoveryCleansOnlyRecognizedTempAudio()
    {
        var root = NewRoot();
        try
        {
            var store = new SqliteSessionStore(Path.Combine(root,"history.db")); var images = new FailingImages();
            var cleaner = new ImageReferenceCleaner(store,images);
            await Assert.ThrowsAsync<IOException>(() => cleaner.ReconcileAsync(default));
            images.Fail = false; await cleaner.ReconcileAsync(default); Assert.Equal(2,images.Calls);
            var temporary = Path.Combine(root,"TempAudio"); Directory.CreateDirectory(temporary);
            var clip = Path.Combine(temporary,Guid.NewGuid().ToString("N") + ".wav"); await File.WriteAllBytesAsync(clip,[1]);
            var keep = Path.Combine(temporary,"user-note.txt"); await File.WriteAllTextAsync(keep,"keep");
            AppDataPaths.CleanTemporaryAudio(temporary);
            Assert.False(File.Exists(clip)); Assert.True(File.Exists(keep));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task ANewImageCommittedAfterTheSweepSnapshotRemainsReadable()
    {
        var root=NewRoot();
        try
        {
            var pending=new ImageReferenceRegistry(); var store=new SqliteSessionStore(Path.Combine(root,"history.db"),pending); var files=new FileImageStore(Path.Combine(root,"Images"),pending);
            var delayed=new DelayedDelete(files); var cleaner=new ImageReferenceCleaner(store,delayed,pending);
            var sweep=cleaner.ReconcileAsync(default); await delayed.Started.Task;
            var image=await files.AddAsync(new([1,2,3],20,10),default);
            var session=new SessionSnapshot { Draft=new("B",[image],1) }; await store.SaveAsync(session,default);
            delayed.Release.TrySetResult(); await sweep;
            Assert.Equal(new byte[] {1,2,3},await files.ReadAsync(image,default));
            await store.DeleteAsync(session.Id,default); await new ImageReferenceCleaner(store,files,pending).ReconcileAsync(default);
            Assert.False(File.Exists(Path.Combine(root,"Images",image.RelativePath)));
        }
        finally { Directory.Delete(root,true); }
    }
    private sealed class DelayedDelete(IImageStore inner) : IImageStore
    {
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously); public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct)=>inner.AddAsync(image,ct);
        public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct)=>inner.ReadAsync(image,ct);
        public async Task DeleteUnreferencedAsync(IReadOnlySet<Guid> ids,CancellationToken ct) { Started.TrySetResult(); await Release.Task.WaitAsync(ct); await inner.DeleteUnreferencedAsync(ids,ct); }
    }
    private static string NewRoot() { var path = Path.Combine(Path.GetTempPath(),"ExamHelper-clean-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(path); return path; }
    private sealed class FailingImages : IImageStore
    {
        public bool Fail = true; public int Calls;
        public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct) => throw new NotSupportedException();
        public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct)
        { Calls++; if(Fail) throw new IOException("simulated interrupted deletion"); return Task.CompletedTask; }
    }
}