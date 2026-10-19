using System.IO;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Persistence;
using Xunit;
namespace ExamHelper.Windows.Tests.Persistence;
public sealed class HistoryChronology04Tests
{
    [Fact]
    public async Task SortingUsesLastMessageCreationEvenAfterSavingOlderChatAgain()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-history04-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new SqliteSessionStore(Path.Combine(root,"history.db"));
            var a=new SessionSnapshot { Id=Guid.Parse("11111111-1111-1111-1111-111111111111"),Messages=[new() { Role=MessageRole.User,Text="A",CreatedAt=DateTimeOffset.Parse("2026-10-18T01:00:00Z") }],UpdatedAt=DateTimeOffset.Parse("2026-10-18T09:00:00Z") };
            var b=new SessionSnapshot { Id=Guid.Parse("22222222-2222-2222-2222-222222222222"),Messages=[new() { Role=MessageRole.User,Text="B",CreatedAt=DateTimeOffset.Parse("2026-10-18T02:00:00Z") }],UpdatedAt=DateTimeOffset.Parse("2026-10-18T08:00:00Z") };
            await store.SaveAsync(a,default); await store.SaveAsync(b,default);
            Assert.Equal(new[] { b.Id,a.Id },(await store.ListAsync(default)).Select(s=>s.Id));
            await store.SaveAsync(a with { CustomTitle="renamed",Draft=new("edited",[],1),UpdatedAt=DateTimeOffset.Parse("2026-10-19T05:00:00Z") },default);
            Assert.Equal(new[] { b.Id,a.Id },(await store.ListAsync(default)).Select(s=>s.Id));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
    [Fact]
    public async Task UnversionedForeignDatabaseIsNotClaimedOrModified()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-foreign04-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var path=Path.Combine(root,"history.db");
        try
        {
            await using(var c=new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={path};Pooling=False"))
            {
                await c.OpenAsync(); using var q=c.CreateCommand(); q.CommandText="CREATE TABLE ForeignData(Value TEXT); INSERT INTO ForeignData VALUES('keep');"; await q.ExecuteNonQueryAsync();
            }
            var original=await File.ReadAllBytesAsync(path);
            await Assert.ThrowsAsync<ExamHelper.Core.Common.ServiceFailure>(()=>new SqliteSessionStore(path).ListAsync(default));
            Assert.Equal(original,await File.ReadAllBytesAsync(path));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task DraftCreationTimeSurvivesEditRenameAndReopen()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-draft-time04-"+Guid.NewGuid().ToString("N"));
        try
        {
            var path=Path.Combine(root,"history.db"); var store=new SqliteSessionStore(path);
            var first=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
            var a=new SessionSnapshot { CreatedAt=first,UpdatedAt=first,Draft=new("A",[],1) };
            var b=new SessionSnapshot { CreatedAt=first.AddMinutes(1),UpdatedAt=first.AddMinutes(1),Draft=new("B",[],1) };
            await store.SaveAsync(a,default); await store.SaveAsync(b,default);
            await store.SaveAsync(a with { CustomTitle="renamed",UpdatedAt=first.AddDays(1),Draft=new("later A",[],2) },default);
            var reopened=new SqliteSessionStore(path); var summaries=await reopened.ListAsync(default);
            Assert.Equal(new[] { b.Id,a.Id },summaries.Select(s=>s.Id));
            Assert.Equal(first,summaries.Single(s=>s.Id==a.Id).SortAt);
            Assert.Equal(first,(await reopened.LoadAsync(a.Id,default))!.CreatedAt);
            Assert.Equal("2026-10-18 08:00:00",ExamHelper.Core.Common.DisplayTime.Format(summaries.Single(s=>s.Id==a.Id).SortAt));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
    [Fact]
    public void LastMessageMeansSequenceOrderNotGreatestTimestampOrSaveTime()
    {
        var t=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        var state=new SessionSnapshot { CreatedAt=t.AddDays(-1),UpdatedAt=t.AddDays(2),Messages=[
            new() { Role=MessageRole.User,CreatedAt=t.AddHours(1) },new() { Role=MessageRole.Assistant,CreatedAt=t },new() { Role=MessageRole.System,CreatedAt=t.AddDays(3) }] };
        Assert.Equal(t,ConversationChronology.SortAt(state));
        Assert.Equal("AI · 2026-10-18 08:00:00",new ExamHelper.App.Home.ConversationMessageViewModel(state.Messages[1] with { Status=MessageStatus.Complete }).Header);
    }
}
