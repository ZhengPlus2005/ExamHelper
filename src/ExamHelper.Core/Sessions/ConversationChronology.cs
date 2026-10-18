namespace ExamHelper.Core.Sessions;
public static class ConversationChronology
{
    public static DateTimeOffset SortAt(SessionSnapshot snapshot) => snapshot.Messages.LastOrDefault(m => m.Role is MessageRole.User or MessageRole.Assistant)?.CreatedAt ?? snapshot.CreatedAt;
}
