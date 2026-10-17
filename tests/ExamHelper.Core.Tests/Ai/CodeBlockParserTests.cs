using ExamHelper.Core.Ai;
using Xunit;
namespace ExamHelper.Core.Tests.Ai;
public class CodeBlockParserTests
{
    [Fact]
    public void IncompleteFenceDoesNotLoseText()
    {
        var partial = CodeBlockParser.Parse("前文\n``",false);
        Assert.Equal("前文\n``",string.Concat(partial.Select(s => s.Text)));
        var code = CodeBlockParser.Parse("前文\n```cpp\nint main() {",false);
        Assert.Equal(AnswerSegmentKind.Code,code[^1].Kind);
        Assert.Equal("cpp",code[^1].Language); Assert.Equal("int main() {",code[^1].Text);
    }
    [Fact]
    public void PlainMarkdownStaysLiteralAndCodeFenceIsSeparated()
    {
        var text = "**答案**：x^2，$x$。\n";
        var parsed = CodeBlockParser.Parse(text + "```cpp\nint x = 1;\n```\n结束",true);
        Assert.Equal(text,parsed[0].Text); Assert.Equal(AnswerSegmentKind.Plain,parsed[0].Kind);
        Assert.Equal("int x = 1;\n",parsed[1].Text); Assert.Equal("结束",parsed[2].Text);
    }
}