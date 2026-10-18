using System.IO;
using System.Text;
using ExamHelper.Core.Export;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Common;
namespace ExamHelper.App.Home;
public sealed class ConversationExportController(HtmlConversationExporter exporter)
{
    public async Task WriteAsync(string path,SessionSnapshot snapshot,CancellationToken ct)
    {
        var html=await exporter.BuildAsync(snapshot,ct);
        await AtomicFile.WriteAsync(path,Encoding.UTF8.GetBytes(html),ct);
    }
    public static string DefaultFileName(SessionSnapshot snapshot) => string.Concat(ConversationTitle.Get(snapshot).Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c))+".html";
}
