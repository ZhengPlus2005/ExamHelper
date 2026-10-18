using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Common;
using ExamHelper.App.Controls;
using ExamHelper.App.Home;
using ExamHelper.Core.Sessions;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class MessagePresentation04Tests
{
    [Fact] public async Task MainWindowClockTicksDoNotRebuildMessagesOrMoveReadingAndBackgroundChatCannotReplaceViewedContent()
    {
        await StaTest.RunAsync(()=>
        {
            var rig=new ExamHelper.TestSupport.ConversationWorkspaceRig();var a=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            var home=new HomeViewModel(rig.Workspace,rig.Store,new ExamHelper.TestSupport.StubServices(),()=>Task.FromResult(rig.Settings),rig.Verification,new Host(),()=>Task.CompletedTask);
            var window=new HomeWindow(home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);window.Show();window.UpdateLayout();StaTest.Pump();
            try
            {
                a.UpdateDraftTextAsync("A",default).GetAwaiter().GetResult();var attempt=rig.Ai.QueueAttempt();a.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default).GetAwaiter().GetResult();attempt.Started.Task.GetAwaiter().GetResult();
                attempt.Text("```cpp\nint x;\n```\n\n"+string.Join("\n\n",Enumerable.Repeat("long paragraph",80)));Wait(()=>a.State.Messages.Last().Text.Length>100);StaTest.Pump();Render(window,true);window.UpdateLayout();StaTest.Pump();
                var panel=Assert.IsType<StackPanel>(window.FindName("HistoryMessages"));var answer=Assert.IsType<HomeMessageView>(panel.Children[1]);var markdown=MarkdownViewTests.Nodes(answer).OfType<MarkdownView>().Single();var body=markdown.Content;var copy=answer.FindName("CopyMessageButton");
                var scroll=Assert.IsType<ScrollViewer>(window.FindName("HistoryScroll"));scroll.ScrollToVerticalOffset(200);window.UpdateLayout();StaTest.Pump();var offset=scroll.VerticalOffset;Assert.True(offset>0);
                var frozen=home.SnapshotForPresentation()!;var revision=home.ViewRevision;
                for(var i=0;i<30;i++) { rig.Clock.Advance(TimeSpan.FromSeconds(1));Render(window,false);window.UpdateLayout(); }
                Assert.Same(answer,panel.Children[1]);Assert.Same(body,markdown.Content);Assert.Same(copy,answer.FindName("CopyMessageButton"));Assert.InRange(scroll.VerticalOffset,offset-1,offset+1);Assert.Equal(revision,home.ViewRevision);
                Assert.Equal("已用 30 秒",Assert.IsType<TextBlock>(answer.FindName("TimingLabel")).Text);Assert.Equal(TimeSpan.Zero,frozen.Messages.Last().Timing!.Elapsed);
                var b=new SessionSnapshot { IsAuxiliaryMode=false,Messages=[new() { Role=MessageRole.User,Text="**B raw**",Status=MessageStatus.Complete }] };rig.Store.SaveAsync(b,default).GetAwaiter().GetResult();home.SelectAsync(b.Id,default).GetAwaiter().GetResult();StaTest.Pump();Render(window,true);window.UpdateLayout();
                var bview=Assert.IsType<HomeMessageView>(panel.Children[0]);rig.Workspace.Selected!.UpdateDraftTextAsync("B draft",default).GetAwaiter().GetResult();attempt.Text("background A");Wait(()=>a.State.Messages.Last().Text.EndsWith("background A"));StaTest.Pump();Render(window,false);
                Assert.Same(bview,panel.Children[0]);Assert.Equal("**B raw**",bview.CopyText);Assert.Equal("B draft",rig.Workspace.Selected.State.Draft.Text);
                Assert.DoesNotContain(MarkdownViewTests.Nodes(window).OfType<Button>(),button=>Equals(button.Content,"清空输入") || Equals(button.Content,"导出 HTML"));Assert.Equal("导出",Assert.IsType<Button>(window.FindName("ExportButton")).Content);
                attempt.Complete();a.WaitForIdleAsync(default).GetAwaiter().GetResult();
            }
            finally { window.Close();StaTest.Pump();rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    private static void Render(HomeWindow window,bool force)=>typeof(HomeWindow).GetMethod("Render",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[typeof(bool)],null)!.Invoke(window,[force]);
    private static void Wait(Func<bool> ready) { var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready() && watch.Elapsed<TimeSpan.FromSeconds(3)) { StaTest.Pump();Thread.Sleep(3); }Assert.True(ready()); }
    private sealed class Host:ExamHelper.App.Overlay.IOverlayHost
    { public Task<ExamHelper.Windows.Native.ProtectionResult> OpenAsync(CancellationToken ct)=>throw new NotSupportedException();public void Show() { }public void Close() { } }
    [Fact] public async Task TimingOnlyUpdatesKeepBodyCopyControlsAndOriginalMarkdown()
    {
        await StaTest.RunAsync(()=>
        {
            var raw="**答案**\n```cpp\nint x;\n```";var message=new MessageSnapshot { Role=MessageRole.Assistant,Text=raw,ReasoningContent="hidden" };
            string? copied=null;var view=new HomeMessageView(copy:text=>copied=text);view.Update(message);view.Measure(new Size(600,1000));view.Arrange(new Rect(0,0,600,1000));view.UpdateLayout();var markdown=MarkdownViewTests.Nodes(view).OfType<MarkdownView>().Single();var body=markdown.Content;var copy=Assert.IsType<Button>(view.FindName("CopyMessageButton"));
            var timer=new ResponseTiming(Guid.NewGuid(),1,DateTimeOffset.UtcNow,TimeSpan.FromSeconds(12.4),true);
            Assert.Equal("已用 12 秒",ResponseTimingFormatter.Format(timer));Assert.Equal("用时 12.4 秒",ResponseTimingFormatter.Format(timer with { IsRunning=false }));Assert.Equal("",ResponseTimingFormatter.Format(null));
            for(var i=0;i<30;i++)view.UpdateTiming(timer with { Elapsed=TimeSpan.FromSeconds(i) });
            Assert.Same(markdown,MarkdownViewTests.Nodes(view).OfType<MarkdownView>().Single());Assert.Same(body,markdown.Content);Assert.Same(copy,view.FindName("CopyMessageButton"));
            Assert.Equal(raw,view.CopyText);copy.Command.Execute(null);Assert.Equal(raw,copied);
            view.Update(message with { Text=raw+"end" });Assert.Equal(raw+"end",view.CopyText);Assert.NotSame(body,markdown.Content);
            view.Update(message with { Role=MessageRole.User,Text="**literal**" });view.UpdateLayout();Assert.Equal("**literal**",view.CopyText);Assert.Empty(MarkdownViewTests.Nodes(view).OfType<MarkdownView>());
        });
    }
}
