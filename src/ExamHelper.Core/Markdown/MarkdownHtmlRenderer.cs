using System.Net;
using System.Text;
using ExamHelper.Core.Export;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Markdig.Extensions.Tables;
namespace ExamHelper.Core.Markdown;
public sealed class MarkdownHtmlRenderer : IMarkdownHtmlRenderer
{
    public string Render(string text) { var output=new StringBuilder(); Blocks(output,MarkdownPipelineFactory.Parse(text)); return output.ToString(); }
    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
    private static void Blocks(StringBuilder output,ContainerBlock blocks)
    {
        foreach(var block in blocks)
        {
            switch(block)
            {
                case HeadingBlock heading: output.Append($"<h{heading.Level}>"); Inlines(output,heading.Inline); output.Append($"</h{heading.Level}>"); break;
                case ParagraphBlock paragraph: output.Append("<p>"); Inlines(output,paragraph.Inline); output.Append("</p>"); break;
                case CodeBlock code:
                    var language=code is FencedCodeBlock fenced ? fenced.Info ?? "" : "";
                    output.Append("<div class=\"code-language\">").Append(E(language)).Append("</div><pre><code>").Append(E(code.Lines.ToString())).Append("</code></pre>"); break;
                case Table table:
                    output.Append("<table>"); foreach(var row in table.OfType<TableRow>()) { output.Append("<tr>"); foreach(var cell in row.OfType<TableCell>()) { var tag=row.IsHeader ? "th" : "td"; output.Append('<').Append(tag).Append('>'); Blocks(output,cell); output.Append("</").Append(tag).Append('>'); } output.Append("</tr>"); } output.Append("</table>"); break;
                case ListBlock list:
                    var listTag=list.IsOrdered ? "ol" : "ul"; output.Append('<').Append(listTag);
                    if(list.IsOrdered) output.Append(" start=\"").Append(E(Convert.ToString(list.OrderedStart,System.Globalization.CultureInfo.InvariantCulture))).Append('"');
                    output.Append('>'); foreach(var item in list.OfType<ListItemBlock>()) { output.Append("<li>"); Blocks(output,item); output.Append("</li>"); } output.Append("</").Append(listTag).Append('>'); break;
                case QuoteBlock quote: output.Append("<blockquote>"); Blocks(output,quote); output.Append("</blockquote>"); break;
                case ThematicBreakBlock: output.Append("<hr>"); break;
                case ContainerBlock nested: Blocks(output,nested); break;
            }
        }
    }
    private static void Inlines(StringBuilder output,ContainerInline? container)
    {
        for(var node=container?.FirstChild;node is not null;node=node.NextSibling)
        {
            switch(node)
            {
                case LiteralInline literal: output.Append(E(literal.Content.ToString())); break;
                case CodeInline code: output.Append("<code>").Append(E(code.Content)).Append("</code>"); break;
                case HtmlEntityInline entity: output.Append(E(entity.Transcoded.ToString())); break;
                case LineBreakInline line: output.Append(line.IsHard ? "<br>" : " "); break;
                case EmphasisInline emphasis: var tag=emphasis.DelimiterCount>=2 ? "strong" : "em"; output.Append('<').Append(tag).Append('>'); Inlines(output,emphasis); output.Append("</").Append(tag).Append('>'); break;
                case LinkInline link:
                    if(!link.IsImage && MarkdownPipelineFactory.TrySafeLink(link.Url,out var uri)) { output.Append("<a rel=\"noreferrer noopener\" href=\"").Append(E(uri!.AbsoluteUri)).Append("\">"); Inlines(output,link); output.Append("</a>"); } else Inlines(output,link); break;
                case AutolinkInline auto:
                    if(MarkdownPipelineFactory.TrySafeLink(auto.Url,out var safe)) output.Append("<a rel=\"noreferrer noopener\" href=\"").Append(E(safe!.AbsoluteUri)).Append("\">").Append(E(auto.Url)).Append("</a>"); else output.Append(E(auto.Url)); break;
                case ContainerInline nested: Inlines(output,nested); break;
            }
        }
    }
}
