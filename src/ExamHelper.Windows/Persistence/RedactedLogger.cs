using System.IO;
using System.Text.Json;
using ExamHelper.Core.Common;
namespace ExamHelper.Windows.Persistence;
public enum DiagnosticEvent { Startup,ServiceTestFailed,CaptureFailed,StorageFailure,CleanupDeferred }
public sealed class RedactedLogger(string directory)
{
    private readonly SemaphoreSlim _gate = new(1);
    public async Task WriteAsync(DiagnosticEvent kind,ServiceFailure? error,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(directory);
            // Log typed metadata only: no exception message, HTTP body, key, transcript or screenshot.
            var code = int.TryParse(error?.Code,out var numeric) ? numeric.ToString() : null;
            var text = JsonSerializer.Serialize(new { Time = DateTimeOffset.UtcNow,Event = kind.ToString(),Category = error?.Category.ToString(),Code = code });
            await File.AppendAllTextAsync(Path.Combine(directory,"diagnostics.jsonl"),text + Environment.NewLine,ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
}