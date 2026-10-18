using System.Net;
using System.Text;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
namespace ExamHelper.Core.Export;
public sealed class HtmlConversationExporter(IImageStore images,IMarkdownHtmlRenderer markdown)
{
    public async Task<string> BuildAsync(SessionSnapshot snapshot,CancellationToken ct)
    {
        var messages=snapshot.Messages.Where(m => m.Role!=MessageRole.System).ToArray();
        var bytes=await images.ReadSnapshotAsync(messages.SelectMany(m => m.Images).DistinctBy(i => i.Id).ToArray(),ct);
        var title=WebUtility.HtmlEncode(ConversationTitle.Get(snapshot));
        var output=new StringBuilder("<!doctype html><html lang=\"zh-CN\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><meta http-equiv=\"Content-Security-Policy\" content=\"default-src 'none'; img-src data:; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'\"><title>");
        output.Append(title).Append("</title><style>").Append(Styles).Append("</style></head><body><main><h1>").Append(title).Append("</h1>");
        foreach(var message in messages)
        {
            ct.ThrowIfCancellationRequested();
            var status=message.Status switch { MessageStatus.Pending => "部分回答",MessageStatus.Failed => "失败",MessageStatus.Stopped => "已停止",_ => "" };
            output.Append("<article class=\"").Append(message.Role==MessageRole.User ? "user" : "assistant").Append("\"><header><b>").Append(message.Role==MessageRole.User ? "我" : "AI").Append("</b> <time datetime=\"").Append(message.CreatedAt.ToString("O")).Append("\">").Append(message.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss zzz")).Append("</time> ").Append(status).Append("</header>");
            foreach(var image in message.Images) output.Append("<img class=\"attachment\" alt=\"对话图片\" src=\"data:image/png;base64,").Append(Convert.ToBase64String(bytes[image.Id])).Append("\">");
            if(message.Role==MessageRole.Assistant) output.Append("<div class=\"markdown\">").Append(markdown.Render(message.Text)).Append("</div>");
            else output.Append("<div class=\"plain\">").Append(WebUtility.HtmlEncode(message.Text)).Append("</div>");
            output.Append("</article>");
        }
        return output.Append("</main></body></html>").ToString();
    }
    private const string Styles="""
        *{box-sizing:border-box}body{margin:0;background:#f3f5f8;color:#192331;font:15px/1.65 'Segoe UI','Microsoft YaHei',sans-serif}main{max-width:1000px;margin:auto;padding:28px 20px}h1{font-size:24px;overflow-wrap:anywhere}article{background:white;border:1px solid #dce3ec;border-radius:12px;padding:20px;margin:18px 0;overflow-wrap:anywhere}article.user{border-left:4px solid #6c9dce}header{font-size:12px;color:#526174;margin-bottom:14px}header b{color:#192331;margin-right:12px}.attachment{display:block;max-width:100%;height:auto;border-radius:10px;margin:12px 0}.plain{white-space:pre-wrap}pre{overflow:auto;background:#f1f4f8;padding:14px;border-radius:6px;line-height:1.5}code{font-family:Consolas,monospace;white-space:pre}p code{background:#f1f4f8;padding:2px 4px}.code-language{font-size:12px;color:#526174}table{border-collapse:collapse;display:block;overflow:auto}td,th{border:1px solid #b9c7d8;padding:7px 12px}th{background:#edf4fc}blockquote{border-left:3px solid #9db3cd;padding-left:14px;color:#526174}a{color:#2865ad}h2,h3{line-height:1.4}@media print{body{background:white}main{padding:0}article{break-inside:avoid}}
        """;
}
