using System.IO;
using ExamHelper.App.Home;
using ExamHelper.Core.Export;
using ExamHelper.Core.Markdown;
using ExamHelper.Core.Sessions;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class ConversationExportControllerTests
{
    [Fact] public async Task MissingImageOrWriteFailureNeverOverwritesTheExistingExport()
    {
        var root=Path.Combine(Path.GetTempPath(),"ExamHelper-export-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); var path=Path.Combine(root,"conversation.html");
        try
        {
            await File.WriteAllTextAsync(path,"original export"); var exporter=new HtmlConversationExporter(new MissingImages(),new MarkdownHtmlRenderer());
            var controller=new ConversationExportController(exporter);
            await Assert.ThrowsAnyAsync<Exception>(() => controller.WriteAsync(path,new() { Messages=[new() { Role=MessageRole.User,Images=[new(Guid.NewGuid(),"missing.png",1,1)] }] },default));
            Assert.Equal("original export",await File.ReadAllTextAsync(path));
            File.SetAttributes(path,FileAttributes.ReadOnly);
            await Assert.ThrowsAnyAsync<Exception>(() => controller.WriteAsync(path,new() { CustomTitle="changed",Messages=[new() { Text="valid" }] },default));
            File.SetAttributes(path,FileAttributes.Normal); Assert.Equal("original export",await File.ReadAllTextAsync(path)); Assert.Empty(Directory.GetFiles(root,"*.tmp"));
        }
        finally { if(File.Exists(path)) File.SetAttributes(path,FileAttributes.Normal); Directory.Delete(root,true); }
    }
    private sealed class MissingImages : ExamHelper.Core.Drafts.IImageStore
    {
        public Task<byte[]> ReadAsync(ExamHelper.Core.Drafts.ImageRef image,CancellationToken ct) => throw new FileNotFoundException();
        public Task<ExamHelper.Core.Drafts.ImageRef> AddAsync(ExamHelper.Core.Drafts.CapturedImage image,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> ids,CancellationToken ct) => Task.CompletedTask;
    }
}
