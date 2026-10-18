using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using ExamHelper.App.Controls;
using ExamHelper.Core.Markdown;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class MarkdownViewTests
{
    [Fact] public async Task RendersRequestedBlocksAndNeverCreatesAnEditableAuxiliaryControl()
    {
        await StaTest.RunAsync(() =>
        {
            var document=MarkdownPipelineFactory.Parse("# 标题\n\n**重点** 与 *斜体*\n\n1. 第一项\n2. 第二项\n\n> 引用\n\n| 列一 | 列二 |\n| --- | --- |\n| 内容 | 42 |\n\n```cpp\nint main() { return 0; }\n```\n");
            var content=MarkdownWpfRenderer.Render(document,true); Prepare(content); var nodes=Nodes(content).ToArray();
            var text=string.Join("\n",nodes.OfType<TextBlock>().Select(TextOf));
            Assert.Contains("标题",text); Assert.Contains("第一项",text); Assert.Contains("引用",text); Assert.Contains("内容",text); Assert.Contains("int main()",text);
            Assert.Contains(nodes.OfType<TextBlock>(),t => t.FontSize>=20); Assert.Contains(nodes,n => n is Grid);
            Assert.DoesNotContain(nodes,n => n is TextBox || n.GetType().Name=="TextEditor");
            Assert.All(nodes.OfType<FrameworkElement>(),n => Assert.False(n.Focusable));
        });
    }
    [Fact] public async Task HtmlAndImagesStayInertAndUnsafeLinksAreNotClickable()
    {
        await StaTest.RunAsync(() =>
        {
            var content=MarkdownWpfRenderer.Render(MarkdownPipelineFactory.Parse("<script>run()</script>\n\n![remote](https://example.com/private.png) [safe](https://example.com) [danger](javascript:run())"),true);
            Prepare(content); var nodes=Nodes(content).ToArray(); Assert.DoesNotContain(nodes,n => n is Image);
            var links=nodes.OfType<TextBlock>().SelectMany(t => Inlines(t.Inlines)).OfType<Hyperlink>().ToArray();
            Assert.Single(links); Assert.Equal("https",links[0].NavigateUri!.Scheme); Assert.False(links[0].Focusable);
        });
    }
    [Fact] public async Task IncompleteFenceAndUnknownLanguageKeepLiteralCodeAndKnownCppIsColored()
    {
        await StaTest.RunAsync(() =>
        {
            var content=MarkdownWpfRenderer.Render(MarkdownPipelineFactory.Parse("```unlisted-language\nx < y && a > b"),false); Prepare(content);
            Assert.Contains(Nodes(content).OfType<TextBlock>(),t => TextOf(t).Contains("x < y && a > b"));
            var runs=CodeHighlighter.Highlight("int main() { return 0; } // comment","cpp",true);
            Assert.Equal("int main() { return 0; } // comment",string.Concat(runs.Select(r => r.Text)));
            Assert.True(runs.Where(r => r.Foreground is not null).Select(r => r.Foreground.ToString()).Distinct().Count()>1);
        });
    }
    [Fact] public async Task StreamingHomeMarkdownKeepsScrollPositionAndUserTextIsLiteral()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ExamHelper.TestSupport.ConversationWorkspaceRig();
            var coordinator=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            var vm=new ExamHelper.App.Home.HomeViewModel(rig.Workspace,rig.Store,new ExamHelper.TestSupport.StubServices(),() => Task.FromResult(rig.Settings),new(new byte[32]),new Host(),() => Task.CompletedTask);
            var window=new ExamHelper.App.Home.HomeWindow(vm,() => throw new NotSupportedException(),() => Task.CompletedTask);
            try
            {
                window.Show(); window.UpdateLayout(); StaTest.Pump();
                coordinator.UpdateDraftTextAsync("**保留用户原文**",default).GetAwaiter().GetResult(); var answer=rig.Ai.QueueAttempt();
                coordinator.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default).GetAwaiter().GetResult(); answer.Started.Task.GetAwaiter().GetResult();
                var markdown="# 示例回答\n\n**答案：A**。理由如下。\n\n| 条件 | 结论 |\n| --- | --- |\n| 时间复杂度 | O(n) |\n\n```cpp\n#include <iostream>\nint main() {\n    // 输出答案\n    std::cout << 42;\n    return 0;\n}\n```\n\n"+string.Join("\n\n",Enumerable.Range(0,40).Select(i => $"段落 {i}：这是用于检查滚动位置的合成回答。"));
                answer.Text(markdown);
                var scroll=Assert.IsType<ScrollViewer>(window.FindName("HistoryScroll")); Wait(() => scroll.ScrollableHeight>700);
                scroll.ScrollToTop(); window.UpdateLayout(); StaTest.Pump();
                var output=Environment.GetEnvironmentVariable("EXAMHELPER_TEST_UI_OUTPUT");
                if(!string.IsNullOrEmpty(output))
                {
                    System.IO.Directory.CreateDirectory(output); var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
                    var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using var file=System.IO.File.Create(System.IO.Path.Combine(output,"home-markdown.png")); png.Save(file);
                }
                scroll.ScrollToVerticalOffset(200); window.UpdateLayout(); StaTest.Pump(); var offset=scroll.VerticalOffset; var extent=scroll.ExtentHeight; Assert.True(offset>0);
                answer.Text("\n\n追加的一段内容。"); Wait(() => scroll.ExtentHeight>extent);
                Assert.InRange(scroll.VerticalOffset,offset-1,offset+1); answer.Complete(); coordinator.WaitForIdleAsync(default).GetAwaiter().GetResult();
                var user=new AnswerView { IsMarkdown=false,Text="**保留用户原文**" }; Prepare(user); Assert.Contains(Nodes(user).OfType<TextBlock>(),t => TextOf(t)=="**保留用户原文**");
            }
            finally { window.Close(); StaTest.Pump(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    private static void Wait(Func<bool> ready)
    { var timer=System.Diagnostics.Stopwatch.StartNew(); while(!ready() && timer.Elapsed<TimeSpan.FromSeconds(4)) { StaTest.Pump(); Thread.Sleep(5); } Assert.True(ready()); }
    private sealed class Host : ExamHelper.App.Overlay.IOverlayHost
    { public Task<ExamHelper.Windows.Native.ProtectionResult> OpenAsync(CancellationToken ct) => throw new NotSupportedException(); public void Show() { } public void Close() { } }
    private static string TextOf(TextBlock text) => new TextRange(text.ContentStart,text.ContentEnd).Text;
    private static void Prepare(FrameworkElement content) { content.Measure(new Size(700,3000)); content.Arrange(new Rect(0,0,700,3000)); content.UpdateLayout(); StaTest.Pump(); }
    public static IEnumerable<DependencyObject> Nodes(DependencyObject root)
    { yield return root; for(var i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) foreach(var child in Nodes(VisualTreeHelper.GetChild(root,i))) yield return child; }
    private static IEnumerable<Inline> Inlines(InlineCollection collection)
    { foreach(var inline in collection) { yield return inline; if(inline is Span span) foreach(var child in Inlines(span.Inlines)) yield return child; } }
}
