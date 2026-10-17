using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Ai;
public static class PromptBuilder
{
    public static string BuildSystem(AppSettings settings) =>
        $"默认使用中文回答。默认编程语言为 {settings.DefaultCodeLanguage}，题目明确指定语言时以题目为准。" +
        "选择题、判断题先给答案，再给简要理由；简答题直接给作答内容；编程题直接给代码，使用代码块并标注语言。" +
        "结合本次全部截图和输入文字理解任务：一道题跨图时合并理解，多道题按截图顺序分别作答。" +
        "普通回答尽量使用纯文本，数学使用 x^2、O(n log n) 等可读文本。不生成图片或 Mermaid。";
}