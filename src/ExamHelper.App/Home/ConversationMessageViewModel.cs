using System.Collections.Immutable;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Home;
public sealed record ConversationMessageViewModel(MessageSnapshot Message)
{
    public ImmutableArray<ImageRef> Images => Message.Images;
    public string Text => Message.Text;
    public string Header => (Message.Role == MessageRole.User ? "我" : "AI") + " · " + Message.CreatedAt.LocalDateTime.ToString("HH:mm:ss") + (Message.Status == MessageStatus.Complete ? "" : " · " + (Message.Status switch { MessageStatus.Pending => "生成中",MessageStatus.Failed => "失败",_ => "已停止" }));
}
