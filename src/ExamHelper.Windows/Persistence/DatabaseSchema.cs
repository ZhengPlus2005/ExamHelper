namespace ExamHelper.Windows.Persistence;
internal static class DatabaseSchema
{
    public const string Create = """
        PRAGMA foreign_keys=ON;
        CREATE TABLE IF NOT EXISTS SchemaVersion(Version INTEGER NOT NULL);
        INSERT INTO SchemaVersion(Version) SELECT 3 WHERE NOT EXISTS(SELECT 1 FROM SchemaVersion);
        CREATE TABLE IF NOT EXISTS Sessions(Id TEXT PRIMARY KEY,Title TEXT NOT NULL,UpdatedAt TEXT NOT NULL,CreatedTicksUtc INTEGER NOT NULL,SortTicksUtc INTEGER NOT NULL,HasSavedDraft INTEGER NOT NULL,SnapshotJson TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Messages(Id TEXT PRIMARY KEY,SessionId TEXT NOT NULL REFERENCES Sessions(Id) ON DELETE CASCADE,Ordinal INTEGER NOT NULL,SnapshotJson TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Requests(RequestId TEXT PRIMARY KEY,SessionId TEXT NOT NULL REFERENCES Sessions(Id) ON DELETE CASCADE,SnapshotJson TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Drafts(SessionId TEXT PRIMARY KEY REFERENCES Sessions(Id) ON DELETE CASCADE,SnapshotJson TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Images(Id TEXT PRIMARY KEY,RelativePath TEXT NOT NULL,Width INTEGER NOT NULL,Height INTEGER NOT NULL);
        CREATE TABLE IF NOT EXISTS ImageLinks(SessionId TEXT NOT NULL REFERENCES Sessions(Id) ON DELETE CASCADE,ImageId TEXT NOT NULL REFERENCES Images(Id),PRIMARY KEY(SessionId,ImageId));
        """;
}