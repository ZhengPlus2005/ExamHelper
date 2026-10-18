using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Persistence;
using Microsoft.Data.Sqlite;
using Xunit;
namespace ExamHelper.Windows.Tests.Persistence;
public class ConversationMigrationTests
{
    [Fact] public async Task MigratesOldSnapshotAfterConsistentBackupAndPreservesReferences()
    {
        var root = Root(); var path = Path.Combine(root,"history.db");
        try
        {
            var store = new SqliteSessionStore(path); var image = new ImageRef(Guid.NewGuid(),"original.png",2560,1440);
            var request = new RequestSnapshot { Text = "failed question",Images = [image],Status = MessageStatus.Failed };
            var snapshot = new SessionSnapshot { Draft = new("draft",[image],8),Requests = [request],PendingRequest = request,Messages = [new() { Role = MessageRole.User,Text = "question",Images = [image],CreatedAt = DateTimeOffset.Parse("2026-10-17T10:11:12+08:00"),Status = MessageStatus.Failed }] };
            await store.SaveAsync(snapshot,default);
            var old = JsonNode.Parse(JsonSerializer.Serialize(snapshot))!.AsObject(); old.Remove("FirstSentAt"); old.Remove("CustomTitle"); old.Remove("Selection");
            await Sql(path,"UPDATE SchemaVersion SET Version=1; UPDATE Sessions SET Title='old title',SnapshotJson=$json",old.ToJsonString());
            var loaded = (await store.LoadAsync(snapshot.Id,default))!;
            Assert.Equal(snapshot.Messages[0].CreatedAt,loaded.FirstSentAt); Assert.Equal("draft",loaded.Draft.Text); Assert.Single(loaded.Draft.Images); Assert.Equal(request.RequestId,loaded.PendingRequest!.RequestId);
            Assert.Equal("对话 "+snapshot.Messages[0].CreatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm:ss"),Assert.Single(await store.ListAsync(default)).Title);
            var backup = Assert.Single(Directory.GetFiles(root,"*.v1.*.bak"));
            Assert.Equal("1",await Scalar(backup,"SELECT Version FROM SchemaVersion"));
            Assert.Equal(old.ToJsonString(),await Scalar(backup,"SELECT SnapshotJson FROM Sessions"));
            Assert.Equal("2",await Scalar(path,"SELECT Version FROM SchemaVersion"));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task EmptySessionsAreRemovedButDraftOnlySessionsSurvive()
    {
        var root = Root(); try
        {
            var store = new SqliteSessionStore(Path.Combine(root,"history.db")); var empty = new SessionSnapshot();
            await store.SaveAsync(empty,default); Assert.Empty(await store.ListAsync(default));
            var draft = empty with { Draft = new("unsent",[],1) }; await store.SaveAsync(draft,default);
            Assert.Equal("对话（未发送）",Assert.Single(await store.ListAsync(default)).Title);
            await store.SaveAsync(empty,default); Assert.Empty(await store.ListAsync(default));
        } finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task FailedMigrationAndUnknownVersionKeepOriginalData()
    {
        var root = Root(); var path = Path.Combine(root,"history.db"); try
        {
            var store = new SqliteSessionStore(path); await store.SaveAsync(new() { Draft = new("keep",[],1) },default);
            await Sql(path,"UPDATE SchemaVersion SET Version=1; UPDATE Sessions SET SnapshotJson='{broken'",null);
            await Assert.ThrowsAsync<ServiceFailure>(() => store.ListAsync(default));
            Assert.Equal("1",await Scalar(path,"SELECT Version FROM SchemaVersion")); Assert.Equal("{broken",await Scalar(path,"SELECT SnapshotJson FROM Sessions"));
            await Sql(path,"UPDATE SchemaVersion SET Version=99",null); await Assert.ThrowsAsync<ServiceFailure>(() => store.ListAsync(default)); Assert.Equal("99",await Scalar(path,"SELECT Version FROM SchemaVersion"));
        } finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task PendingAndMemoryImagesSurviveCleanupUntilPersistedReferencesAreReleased()
    {
        var root = Root(); try
        {
            var registry = new ImageReferenceRegistry(); var store = new SqliteSessionStore(Path.Combine(root,"history.db"),registry);
            var images = new ExamHelper.Windows.Capture.FileImageStore(Path.Combine(root,"images"),registry);
            var pending = await images.AddAsync(new CapturedImage([1],1,1),default);
            var memory = new HashSet<Guid> { pending.Id };
            var cleaner = new ImageReferenceCleaner(store,images,registry,() => memory);
            await cleaner.ReconcileAsync(default); Assert.Equal(new byte[] { 1 },await images.ReadAsync(pending,default));
            var snapshot = new SessionSnapshot { Draft = new("",[pending],1) }; await store.SaveAsync(snapshot,default);
            memory.Clear();
            await store.DeleteAsync(snapshot.Id,default); await cleaner.ReconcileAsync(default);
            await Assert.ThrowsAsync<FileNotFoundException>(() => images.ReadAsync(pending,default));
        } finally { Directory.Delete(root,true); }
    }
    [Fact] public async Task BackupFailureDoesNotChangeOldSchemaOrSnapshot()
    {
        var root = Root(); var path = Path.Combine(root,"history.db"); try
        {
            var store = new SqliteSessionStore(path); await store.SaveAsync(new() { Draft = new("keep",[],1) },default); await Sql(path,"UPDATE SchemaVersion SET Version=1",null);
            await using var c = new SqliteConnection($"Data Source={path};Pooling=False"); await c.OpenAsync();
            await Assert.ThrowsAnyAsync<Exception>(() => SessionMigration.EnsureAsync(c,Path.Combine(root,"missing-parent","db"),default));
            Assert.Equal("1",await Scalar(path,"SELECT Version FROM SchemaVersion")); Assert.Single(await ReadSnapshots(path));
        } finally { Directory.Delete(root,true); }
    }
    private static async Task<List<string>> ReadSnapshots(string path)
    { await using var c = new SqliteConnection($"Data Source={path};Pooling=False"); await c.OpenAsync(); using var q=c.CreateCommand(); q.CommandText="SELECT SnapshotJson FROM Sessions"; var rows = new List<string>(); await using var r=await q.ExecuteReaderAsync(); while(await r.ReadAsync()) rows.Add(r.GetString(0)); return rows; }
    private static string Root() { var p = Path.Combine(Path.GetTempPath(),"ExamHelper-migration-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(p); return p; }
    private static async Task Sql(string path,string sql,string? json)
    { await using var c = new SqliteConnection($"Data Source={path};Pooling=False"); await c.OpenAsync(); using var q = c.CreateCommand(); q.CommandText=sql; if(json is not null) q.Parameters.AddWithValue("$json",json); await q.ExecuteNonQueryAsync(); }
    private static async Task<string> Scalar(string path,string sql)
    { await using var c = new SqliteConnection($"Data Source={path};Pooling=False"); await c.OpenAsync(); using var q=c.CreateCommand(); q.CommandText=sql; return (await q.ExecuteScalarAsync())!.ToString()!; }
}
