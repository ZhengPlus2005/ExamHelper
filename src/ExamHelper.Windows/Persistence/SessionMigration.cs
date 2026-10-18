using System.IO;
using System.Text.Json;
using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
using Microsoft.Data.Sqlite;
namespace ExamHelper.Windows.Persistence;
public static class SessionMigration
{
    public static async Task EnsureAsync(SqliteConnection connection,string databasePath,CancellationToken ct)
    {
        using var exists = connection.CreateCommand(); exists.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
        if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0)
        {
            using var transaction = connection.BeginTransaction(); using var create = connection.CreateCommand();
            create.Transaction = transaction; create.CommandText = DatabaseSchema.Create; await create.ExecuteNonQueryAsync(ct).ConfigureAwait(false); transaction.Commit(); return;
        }
        using var version = connection.CreateCommand(); version.CommandText = "SELECT Version FROM SchemaVersion";
        var current = Convert.ToInt32(await version.ExecuteScalarAsync(ct).ConfigureAwait(false));
        if(current == 2) return;
        if(current != 1) throw new ServiceFailure(FailureCategory.Storage,"历史数据库版本不受支持，原文件已保留。");
        await BackupAsync(connection,databasePath,ct).ConfigureAwait(false);
        using var migration = connection.BeginTransaction();
        var snapshots = new List<SessionSnapshot>();
        using(var query = connection.CreateCommand())
        {
            query.Transaction = migration; query.CommandText = "SELECT SnapshotJson FROM Sessions";
            await using var reader = await query.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while(await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var snapshot = JsonSerializer.Deserialize<SessionSnapshot>(reader.GetString(0)) ?? throw new JsonException("缺少历史快照。");
                snapshots.Add(snapshot with { FirstSentAt = ConversationTitle.FirstSend(snapshot) });
            }
        }
        foreach(var snapshot in snapshots)
        {
            using var update = connection.CreateCommand(); update.Transaction = migration;
            update.Parameters.AddWithValue("$id",snapshot.Id.ToString());
            if(ConversationTitle.IsEmpty(snapshot)) update.CommandText = "DELETE FROM Sessions WHERE Id=$id";
            else
            {
                update.CommandText = "UPDATE Sessions SET Title=$title,SnapshotJson=$json WHERE Id=$id";
                update.Parameters.AddWithValue("$title",ConversationTitle.Get(snapshot)); update.Parameters.AddWithValue("$json",JsonSerializer.Serialize(snapshot));
            }
            await update.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        using var finish = connection.CreateCommand(); finish.Transaction = migration; finish.CommandText = "UPDATE SchemaVersion SET Version=2";
        await finish.ExecuteNonQueryAsync(ct).ConfigureAwait(false); ct.ThrowIfCancellationRequested(); migration.Commit();
    }
    private static async Task BackupAsync(SqliteConnection source,string databasePath,CancellationToken ct)
    {
        var backup = Path.GetFullPath(databasePath) + ".v1." + Guid.NewGuid().ToString("N") + ".bak";
        var temporary = backup + ".tmp";
        try
        {
            ct.ThrowIfCancellationRequested();
            await using(var destination = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = temporary,Pooling = false }.ToString()))
            { await destination.OpenAsync(ct).ConfigureAwait(false); source.BackupDatabase(destination); }
            ct.ThrowIfCancellationRequested(); File.Move(temporary,backup);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}
