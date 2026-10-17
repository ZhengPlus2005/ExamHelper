using System.IO;
using System.Text.Json;
using System.Windows;
using ExamHelper.Windows.Native;
using Microsoft.Data.Sqlite;
namespace ExamHelper.App.Common;
internal static class StartupDiagnostics
{
    public static async Task<int> RunAsync(string path)
    {
        object result; int exit = 0;
        try
        {
            var platform = WindowsPlatform.Current();
            using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync();
            using var query = connection.CreateCommand(); query.CommandText = "SELECT sqlite_version()"; var sqlite = (string)(await query.ExecuteScalarAsync())!;
            var resource = Application.GetResourceStream(new Uri("pack://application:,,,/ExamHelper.App;component/Assets/service-test.png"));
            using var stream = resource.Stream; using var memory = new MemoryStream(); await stream.CopyToAsync(memory);
            var marker = memory.Length > 100 && memory.ToArray().AsSpan(0,4).SequenceEqual(new byte[] {137,80,78,71});
            bool passed = WindowsPlatform.IsSupported(platform) && marker && sqlite.Length > 0;
            result = new { Passed = passed,Platform = platform,Runtime = Environment.Version.ToString(),SQLite = sqlite,BuiltInMarkerAvailable = marker,NetworkRequests = 0,UserSettingsRead = false };
            if(!passed) exit = 1;
        }
        catch(Exception error) { result = new { Passed = false,ErrorType = error.GetType().Name,NetworkRequests = 0,UserSettingsRead = false }; exit = 1; }
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await File.WriteAllTextAsync(full,JsonSerializer.Serialize(result,new JsonSerializerOptions { WriteIndented = true }));
        return exit;
    }
}