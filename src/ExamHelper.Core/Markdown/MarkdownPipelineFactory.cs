using Markdig;
using Markdig.Syntax;
namespace ExamHelper.Core.Markdown;
public static class MarkdownPipelineFactory
{
    private static readonly MarkdownPipeline Pipeline=new MarkdownPipelineBuilder().DisableHtml().UsePipeTables().Build();
    public static MarkdownDocument Parse(string text) => Markdig.Markdown.Parse(text ?? "",Pipeline);
    public static bool TrySafeLink(string? value,out Uri? uri)
    {
        uri=null; if(!Uri.TryCreate(value,UriKind.Absolute,out var parsed) || parsed.Scheme is not ("http" or "https") || string.IsNullOrWhiteSpace(parsed.Host) || !string.IsNullOrEmpty(parsed.UserInfo)) return false;
        uri=parsed; return true;
    }
}
