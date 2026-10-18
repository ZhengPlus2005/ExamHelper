using ExamHelper.Core.Common;
using Microsoft.Data.Sqlite;
namespace ExamHelper.Windows.Persistence;
public static class SessionMigration
{
    public static async Task EnsureAsync(SqliteConnection connection,string databasePath,CancellationToken ct)
    {
        using var exists=connection.CreateCommand();
        exists.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='SchemaVersion'";
        if(Convert.ToInt32(await exists.ExecuteScalarAsync(ct).ConfigureAwait(false))==0)
        {
            using var objects=connection.CreateCommand(); objects.CommandText="SELECT COUNT(*) FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'";
            if(Convert.ToInt32(await objects.ExecuteScalarAsync(ct).ConfigureAwait(false))>0) throw Unsupported();
            using var transaction=connection.BeginTransaction(); using var create=connection.CreateCommand();
            create.Transaction=transaction; create.CommandText=DatabaseSchema.Create;
            await create.ExecuteNonQueryAsync(ct).ConfigureAwait(false); transaction.Commit(); return;
        }
        using var version=connection.CreateCommand(); version.CommandText="SELECT Version FROM SchemaVersion";
        await using var reader=await version.ExecuteReaderAsync(ct).ConfigureAwait(false);
        if(!await reader.ReadAsync(ct).ConfigureAwait(false) || reader.GetInt32(0)!=3 || await reader.ReadAsync(ct).ConfigureAwait(false)) throw Unsupported();
    }
    private static ServiceFailure Unsupported()=>new(FailureCategory.Storage,"历史数据库不属于 0.4 系列或版本无效，原文件已保留。");
}
