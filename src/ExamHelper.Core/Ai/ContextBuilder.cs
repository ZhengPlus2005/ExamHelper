using System.Collections.Immutable;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Ai;
public static class ContextBuilder
{
    public static string RequestText(RequestSnapshot request) =>
        !request.IsDirectCapture || request.Prompt.Length == 0 ? request.Text :
        request.Text.Length == 0 ? request.Prompt : request.Prompt + Environment.NewLine + request.Text;
    public static AiRequest Build(SessionSnapshot session, RequestSnapshot request, AppSettings settings)
    {
        var messages = ImmutableArray.CreateBuilder<AiMessage>();
        messages.Add(new(MessageRole.System, request.SystemPrompt.Length > 0 ? request.SystemPrompt : PromptBuilder.BuildSystem(settings), []));
        var usable = session.Messages.Where(m => m.Role == MessageRole.Assistant && m.Status is MessageStatus.Complete or MessageStatus.Stopped)
            .Select(m => m.RequestId).ToHashSet();
        foreach (var message in session.Messages)
        {
            if (message.RequestId == request.RequestId || !usable.Contains(message.RequestId) || message.Status is MessageStatus.Failed or MessageStatus.Pending) continue;
            messages.Add(new(message.Role,message.Text,message.Images));
        }
        messages.Add(new(MessageRole.User,RequestText(request),request.Images));
        return new(request.RequestId,messages.ToImmutable());
    }
}