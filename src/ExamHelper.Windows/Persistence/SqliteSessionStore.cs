using System.IO;
using System.Text.Json;
using System.Collections.Immutable;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using Microsoft.Data.Sqlite;
namespace ExamHelper.Windows.Persistence;
public sealed class SqliteSessionStore(string path) : ISessionStore
{
    private readonly string _path = Path.GetFullPath(path);
    private readonly SemaphoreSlim _gate = new(1);
    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = _path,Mode = SqliteOpenMode.ReadWriteCreate,ForeignKeys = true,Pooling = false }.ToString());
        try
        {
            await connection.OpenAsync(ct).ConfigureAwait(false);
            using var schema = connection.CreateCommand(); schema.CommandText = DatabaseSchema.Create; await schema.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            using var version = connection.CreateCommand(); version.CommandText = "SELECT Version FROM SchemaVersion";
            if (Convert.ToInt32(await version.ExecuteScalarAsync(ct).ConfigureAwait(false)) != 1)
                throw new ServiceFailure(FailureCategory.Storage,"历史数据库版本不受支持，原文件已保留。");
            return connection;
        }
        catch { await connection.DisposeAsync().ConfigureAwait(false); throw; }
    }
    public async Task SaveAsync(SessionSnapshot snapshot,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(ct).ConfigureAwait(false);
            using var transaction = connection.BeginTransaction();
            await ExecuteAsync(connection,transaction,"""
                INSERT INTO Sessions(Id,Title,UpdatedAt,HasSavedDraft,SnapshotJson) VALUES($id,$title,$updated,$draft,$json)
                ON CONFLICT(Id) DO UPDATE SET Title=$title,UpdatedAt=$updated,HasSavedDraft=$draft,SnapshotJson=$json;
                """,ct,("$id",snapshot.Id.ToString()),("$title",Title(snapshot)),("$updated",snapshot.UpdatedAt.ToString("O")),("$draft",snapshot.Draft.HasContent ? 1 : 0),("$json",JsonSerializer.Serialize(snapshot))).ConfigureAwait(false);
            foreach(var table in new[] { "Messages","Requests","Drafts","ImageLinks" })
                await ExecuteAsync(connection,transaction,$"DELETE FROM {table} WHERE SessionId=$id",ct,("$id",snapshot.Id.ToString())).ConfigureAwait(false);
            for(int index = 0;index < snapshot.Messages.Length;index++)
            {
                var message = snapshot.Messages[index];
                await ExecuteAsync(connection,transaction,"INSERT INTO Messages VALUES($id,$session,$ordinal,$json)",ct,("$id",message.Id.ToString()),("$session",snapshot.Id.ToString()),("$ordinal",index),("$json",JsonSerializer.Serialize(message))).ConfigureAwait(false);
            }
            var requests = snapshot.PendingRequest is { } pending
                ? snapshot.Requests.Where(r => r.RequestId != pending.RequestId).Append(pending) : snapshot.Requests;
            foreach(var request in requests)
                await ExecuteAsync(connection,transaction,"INSERT INTO Requests VALUES($id,$session,$json)",ct,("$id",request.RequestId.ToString()),("$session",snapshot.Id.ToString()),("$json",JsonSerializer.Serialize(request))).ConfigureAwait(false);
            await ExecuteAsync(connection,transaction,"INSERT INTO Drafts VALUES($session,$json)",ct,("$session",snapshot.Id.ToString()),("$json",JsonSerializer.Serialize(snapshot.Draft))).ConfigureAwait(false);
            foreach(var image in ImageReferences(snapshot).DistinctBy(i => i.Id))
            {
                await ExecuteAsync(connection,transaction,"INSERT OR IGNORE INTO Images VALUES($id,$path,$width,$height)",ct,("$id",image.Id.ToString()),("$path",image.RelativePath),("$width",image.Width),("$height",image.Height)).ConfigureAwait(false);
                await ExecuteAsync(connection,transaction,"INSERT INTO ImageLinks VALUES($session,$image)",ct,("$session",snapshot.Id.ToString()),("$image",image.Id.ToString())).ConfigureAwait(false);
            }
            transaction.Commit();
        }
        catch(Exception error) when(IsStorageFailure(error)) { throw Storage("历史保存失败，内存中的内容仍应保留。",error); }
        finally { _gate.Release(); }
    }
    public async Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(ct).ConfigureAwait(false);
            using var command = connection.CreateCommand(); command.CommandText = "SELECT SnapshotJson FROM Sessions WHERE Id=$id"; command.Parameters.AddWithValue("$id",id.ToString());
            var json = await command.ExecuteScalarAsync(ct).ConfigureAwait(false) as string;
            return json is null ? null : JsonSerializer.Deserialize<SessionSnapshot>(json) ?? throw new JsonException();
        }
        catch(Exception error) when(IsStorageFailure(error)) { throw Storage("无法读取历史，原文件已保留。",error); }
        finally { _gate.Release(); }
    }
    public async Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(ct).ConfigureAwait(false);
            using var command = connection.CreateCommand(); command.CommandText = "SELECT Id,Title,UpdatedAt,HasSavedDraft FROM Sessions ORDER BY UpdatedAt DESC";
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            var result = new List<SessionSummary>();
            while(await reader.ReadAsync(ct).ConfigureAwait(false))
                result.Add(new(Guid.Parse(reader.GetString(0)),reader.GetString(1),DateTimeOffset.Parse(reader.GetString(2)),reader.GetInt32(3) != 0));
            return result;
        }
        catch(Exception error) when(IsStorageFailure(error)) { throw Storage("无法列出历史，原文件已保留。",error); }
        finally { _gate.Release(); }
    }
    public async Task DeleteAsync(Guid id,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var connection = await OpenAsync(ct).ConfigureAwait(false);
            using var transaction = connection.BeginTransaction();
            await ExecuteAsync(connection,transaction,"DELETE FROM Sessions WHERE Id=$id",ct,("$id",id.ToString())).ConfigureAwait(false);
            transaction.Commit();
        }
        catch(Exception error) when(IsStorageFailure(error)) { throw Storage("无法删除对话，请重试。",error); }
        finally { _gate.Release(); }
    }
    public async Task RecoverInterruptedAsync(CancellationToken ct)
    {
        foreach(var summary in await ListAsync(ct).ConfigureAwait(false))
        {
            var session = await LoadAsync(summary.Id,ct).ConfigureAwait(false);
            if(session is not null) await SaveAsync(RequestRecovery.RecoverOnStartup(session),ct).ConfigureAwait(false);
        }
    }
    public static IEnumerable<ImageRef> ImageReferences(SessionSnapshot snapshot) =>
        snapshot.Messages.SelectMany(m => m.Images).Concat(snapshot.Draft.Images)
            .Concat(snapshot.Requests.SelectMany(r => r.Images)).Concat(snapshot.PendingRequest?.Images ?? []);
    private static string Title(SessionSnapshot snapshot)
    {
        var first = snapshot.Messages.FirstOrDefault(m => m.Role == MessageRole.User);
        if(first is not null && !string.IsNullOrWhiteSpace(first.Text) && !snapshot.Requests.Any(r => r.RequestId == first.RequestId && r.IsDirectCapture))
            return string.Concat(first.Text.Trim().EnumerateRunes().Take(40).Select(r => r.ToString()));
        return "截图对话 " + snapshot.UpdatedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    }
    private static async Task ExecuteAsync(SqliteConnection connection,SqliteTransaction transaction,string sql,CancellationToken ct,params (string Name,object Value)[] parameters)
    {
        using var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        foreach(var parameter in parameters) command.Parameters.AddWithValue(parameter.Name,parameter.Value);
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
    private static bool IsStorageFailure(Exception error) => error is SqliteException or IOException or UnauthorizedAccessException or JsonException or FormatException;
    private static ServiceFailure Storage(string message,Exception error) => new(FailureCategory.Storage,message,inner:error);
}