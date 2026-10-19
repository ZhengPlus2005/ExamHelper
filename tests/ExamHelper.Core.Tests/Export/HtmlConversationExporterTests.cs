using ExamHelper.Core.Drafts;
using ExamHelper.Core.Export;
using ExamHelper.Core.Markdown;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Export;
public class HtmlConversationExporterTests
{
    [Fact] public async Task ImagesStayBeforeTheirOwnMessageAndDraftReasoningAndConfigurationAreExcluded()
    {
        var images=new Images(); var a=images.Add(1); var b=images.Add(2); var c=images.Add(3); var draft=images.Add(9);
        var state=new SessionSnapshot { CustomTitle="标题 <safe>",Selection=new(Guid.NewGuid(),Guid.NewGuid(),new()) { ProfileName="PRIVATE_CONFIGURATION" },Draft=new("DRAFT_SECRET",[draft],1),Requests=[new() { SystemPrompt="PRIVATE_SYSTEM_POLICY" }],Messages=[
            new() { Role=MessageRole.User,Text="first <question>",Images=[a,b],CreatedAt=DateTimeOffset.Parse("2026-10-18T10:11:12+08:00") },
            new() { Role=MessageRole.Assistant,Text="# Answer\n\n**A**\n\n```cpp\nint value = 42;\n```",ReasoningContent="PRIVATE_REASONING",Status=MessageStatus.Pending },
            new() { Role=MessageRole.User,Text="second question",Images=[c] }] };
        var html=await new HtmlConversationExporter(images,new MarkdownHtmlRenderer()).BuildAsync(state,default);
        var first=html.IndexOf("first &lt;question&gt;",StringComparison.Ordinal); var second=html.IndexOf("second question",StringComparison.Ordinal);
        Assert.True(html.IndexOf("data:image/png;base64,AQ==",StringComparison.Ordinal)<first); Assert.True(html.IndexOf("data:image/png;base64,Ag==",StringComparison.Ordinal)<first);
        Assert.InRange(html.IndexOf("data:image/png;base64,Aw==",StringComparison.Ordinal),first,second);
        Assert.Contains("标题 &lt;safe&gt;",html); Assert.Contains("10:11:12",html); Assert.Contains("<h1",html); Assert.Contains("<strong>A</strong>",html); Assert.Contains("<pre",html); Assert.Contains("int value = 42;",html);
        Assert.DoesNotContain("DRAFT_SECRET",html); Assert.DoesNotContain("PRIVATE_REASONING",html); Assert.DoesNotContain("PRIVATE_CONFIGURATION",html); Assert.DoesNotContain("PRIVATE_SYSTEM_POLICY",html); Assert.DoesNotContain(draft.Id,images.Reads);
        Assert.DoesNotContain("用时 ",html); Assert.DoesNotContain("已用 ",html);
        Assert.Equal("DRAFT_SECRET",state.Draft.Text); Assert.Equal("PRIVATE_REASONING",state.Messages[1].ReasoningContent);
    }
    [Fact] public void HtmlAndRemoteImagesAreInertAndDangerousLinksNeverBecomeHref()
    {
        var html=new MarkdownHtmlRenderer().Render("<script>attack()</script>\n\n![image](https://example.com/image.png) [safe](https://example.com) [bad](javascript:attack()) [local](file:///C:/bad.exe)");
        Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("<img",html,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("href=\"https://example.com/\"",html); Assert.DoesNotContain("href=\"javascript:",html,StringComparison.OrdinalIgnoreCase); Assert.DoesNotContain("href=\"file:",html,StringComparison.OrdinalIgnoreCase);
    }
    [Fact] public async Task MissingMessageImageFailsRatherThanSilentlyExportingAnIncompleteConversation()
    {
        var state=new SessionSnapshot { Messages=[new() { Role=MessageRole.User,Images=[new(Guid.NewGuid(),"missing.png",1,1)] }] };
        await Assert.ThrowsAnyAsync<Exception>(() => new HtmlConversationExporter(new Images(),new MarkdownHtmlRenderer()).BuildAsync(state,default));
    }
    [Theory]
    [InlineData(true,"已用 12 秒")]
    [InlineData(false,"用时 12.4 秒")]
    public async Task TimingIsFrozenMetadataAndDisplayTimeUsesUtc8(bool running,string expected)
    {
        var id=Guid.NewGuid(); var t=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        var state=new SessionSnapshot { Messages=[new() { RequestId=id,Role=MessageRole.Assistant,Text="answer",CreatedAt=t,Timing=new(id,1,t,TimeSpan.FromSeconds(12.4),running) }] };
        var html=await new HtmlConversationExporter(new Images(),new MarkdownHtmlRenderer()).BuildAsync(state,default);
        Assert.Contains(expected,html); Assert.Contains("2026-10-18 08:00:00</time>",html);Assert.Contains("2026-10-18T08:00:00.0000000+08:00",html); Assert.DoesNotContain("<script",html,StringComparison.OrdinalIgnoreCase);
        Assert.Equal("answer",state.Messages[0].Text);
    }
    private sealed class Images : IImageStore
    {
        private readonly Dictionary<Guid,byte[]> _bytes=[]; public HashSet<Guid> Reads { get; }=[];
        public ImageRef Add(byte value) { var image=new ImageRef(Guid.NewGuid(),"private-path.png",100,60); _bytes[image.Id]=[value]; return image; }
        public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct) { Reads.Add(image.Id); return _bytes.TryGetValue(image.Id,out var bytes) ? Task.FromResult(bytes.ToArray()) : throw new FileNotFoundException(); }
        public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> ids,CancellationToken ct) => Task.CompletedTask;
    }
}
