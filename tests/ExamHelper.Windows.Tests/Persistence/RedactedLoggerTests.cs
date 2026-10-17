using System.IO;
using ExamHelper.Core.Common;
using ExamHelper.Windows.Persistence;
using Xunit;
namespace ExamHelper.Windows.Tests.Persistence;
public class RedactedLoggerTests
{
    [Fact]
    public async Task DiagnosticsNeverPersistFreeFormErrorMessageOrNonNumericCode()
    {
        var root = Path.Combine(Path.GetTempPath(),"ExamHelper-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var logger = new RedactedLogger(root);
            await logger.WriteAsync(DiagnosticEvent.ServiceTestFailed,new ServiceFailure(FailureCategory.Authentication,"sk-secret-USER-CONTENT","sk-secret-CODE"),default);
            var log = await File.ReadAllTextAsync(Path.Combine(root,"diagnostics.jsonl"));
            Assert.DoesNotContain("sk-secret",log); Assert.DoesNotContain("USER-CONTENT",log);
            Assert.Contains("Authentication",log); Assert.Contains("ServiceTestFailed",log);
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}