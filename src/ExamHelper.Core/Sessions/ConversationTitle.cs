using ExamHelper.Core.Common;
namespace ExamHelper.Core.Sessions;
public static class ConversationTitle
{
    public static DateTimeOffset? FirstSend(SessionSnapshot snapshot) => snapshot.FirstSentAt ?? snapshot.Messages.FirstOrDefault(m => m.Role == MessageRole.User)?.CreatedAt;
    public static string Get(SessionSnapshot snapshot) => !string.IsNullOrWhiteSpace(snapshot.CustomTitle) ? snapshot.CustomTitle :
        FirstSend(snapshot) is { } first ? "对话 " + DisplayTime.Format(first) : "对话（未发送）";
    public static bool IsEmpty(SessionSnapshot snapshot) => snapshot.Messages.Length == 0 && !snapshot.Draft.HasContent;
}
