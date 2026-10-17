using System.Collections.Immutable;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Ai;
public class PromptBuilderTests
{
    [Fact]
    public void DirectCaptureUsesPresetOrdinarySendDoesNot()
    {
        var settings = new AppSettings { DirectCapturePrompt = "仅直发专用" };
        var request = new RequestSnapshot { Text = "问题", Prompt = settings.DirectCapturePrompt };
        var ordinary = ContextBuilder.Build(new(),request,settings);
        Assert.Equal("问题",ordinary.Messages[^1].Text);
        var direct = ContextBuilder.Build(new(),request with { IsDirectCapture = true },settings);
        Assert.Contains("仅直发专用",direct.Messages[^1].Text);
    }
    [Fact]
    public void DefaultLanguageIsCppAndExplicitProblemLanguageTakesPriority()
    {
        var text = PromptBuilder.BuildSystem(new());
        Assert.Contains("中文",text); Assert.Contains("C++",text); Assert.Contains("题目明确",text);
        Assert.Contains("Python",PromptBuilder.BuildSystem(new AppSettings { DefaultCodeLanguage = "Python" }));
    }
    [Fact]
    public void RetryDoesNotDuplicateUserMessageAndFailedHistoryIsExcluded()
    {
        var request = new RequestSnapshot { Text = "当前问题" };
        var badId = Guid.NewGuid();
        var state = new SessionSnapshot { Messages = [
            new() { RequestId = badId, Role = MessageRole.User, Text = "失败问题", Status = MessageStatus.Failed },
            new() { RequestId = badId, Role = MessageRole.Assistant, Text = "失败片段", Status = MessageStatus.Failed },
            new() { RequestId = request.RequestId, Role = MessageRole.User, Text = "当前问题", Status = MessageStatus.Pending }] };
        var result = ContextBuilder.Build(state,request,new());
        Assert.Single(result.Messages.Where(m => m.Role == MessageRole.User));
        Assert.DoesNotContain(result.Messages,m => m.Text.Contains("失败"));
    }
    [Fact]
    public void CompletedAndStoppedContextKeepImageOrderAndFrozenSystemPrompt()
    {
        var old = Guid.NewGuid(); var a = new ImageRef(Guid.NewGuid(),"a.png",10,10); var b = a with { Id = Guid.NewGuid(), RelativePath = "b.png" };
        var state = new SessionSnapshot { Messages = [
            new() { RequestId = old, Role = MessageRole.User, Text = "旧题", Images = [a,b], Status = MessageStatus.Complete },
            new() { RequestId = old, Role = MessageRole.Assistant, Text = "部分答案", Status = MessageStatus.Stopped }] };
        var request = new RequestSnapshot { Text = "继续", SystemPrompt = "发送时的基础提示" };
        var result = ContextBuilder.Build(state,request,new());
        Assert.Equal("发送时的基础提示",result.Messages[0].Text);
        Assert.Equal(new[] {a,b},result.Messages[1].Images);
        Assert.Equal("部分答案",result.Messages[2].Text);
    }
}