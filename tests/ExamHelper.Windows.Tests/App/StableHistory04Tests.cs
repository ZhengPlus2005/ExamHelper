using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Sessions;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class StableHistory04Tests
{
    [Fact]
    public async Task UnchangedHistoryRefreshDoesNotResetItems()
    {
        await using var rig=new ConversationWorkspaceRig();
        var runtime=await rig.Workspace.NewAsync(false,default); await runtime.UpdateDraftTextAsync("draft",default); await runtime.FlushAsync(default);
        var home=CreateHome(rig); await home.RefreshAsync(default); var original=Assert.Single(home.History); var resets=0;
        home.History.CollectionChanged+=(_,e)=> { if(e.Action==NotifyCollectionChangedAction.Reset) resets++; };
        for(var i=0;i<20;i++) await home.RefreshAsync(default);
        Assert.Equal(0,resets); Assert.Same(original,Assert.Single(home.History));
    }
    [Fact]
    public async Task ActualHistorySelectionDoesNotBlinkDuringStreamRefresh()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig();
            var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            runtime.UpdateDraftTextAsync("question",default).GetAwaiter().GetResult(); runtime.FlushAsync(default).GetAwaiter().GetResult();
            var home=CreateHome(rig);
            var window=new HomeWindow(home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);
            try
            {
                window.Show(); StaTest.Pump();
                var list=Assert.IsType<ListBox>(window.FindName("HistoryList")); var selected=list.SelectedItem; Assert.NotNull(selected); var removed=0;
                list.SelectionChanged+=(_,e)=>removed+=e.RemovedItems.Count;
                var attempt=rig.Ai.QueueAttempt(); runtime.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default).GetAwaiter().GetResult();
                attempt.Started.Task.GetAwaiter().GetResult();
                for(var i=0;i<20;i++)
                {
                    attempt.Text("part "+i); rig.Clock.Advance(TimeSpan.FromMilliseconds(500));
                    home.RefreshAsync(default).GetAwaiter().GetResult(); StaTest.Pump();
                }
                attempt.Complete(); runtime.WaitForIdleAsync(default).GetAwaiter().GetResult();
                Assert.Equal(0,removed); Assert.Same(selected,list.SelectedItem);
            }
            finally { window.Close(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact]
    public void CollectionUsesStableTieBreakAndReusesSelectionAcrossMovesAndRenames()
    {
        var t=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        var a=new SessionSummary(Guid.Parse("11111111-1111-1111-1111-111111111111"),"A",t,true) { CreatedAt=t,SortAt=t };
        var b=new SessionSummary(Guid.Parse("22222222-2222-2222-2222-222222222222"),"B",t,true) { CreatedAt=t,SortAt=t };
        var list=new HistoryCollection(); list.Apply([b,a]); var selected=list.Single(i=>i.Id==b.Id);
        Assert.Equal(new[] { a.Id,b.Id },list.Select(i=>i.Id)); var resets=0;
        list.CollectionChanged+=(_,e)=> { if(e.Action==NotifyCollectionChangedAction.Reset) resets++; };
        list.Apply([a,b with { Title="renamed B",UpdatedAt=t.AddDays(2) }]);
        Assert.Same(selected,list[1]); Assert.Equal("renamed B",selected.Title); Assert.Equal("2026-10-18 08:00:00",selected.TimeText);
        list.Apply([a,b with { SortAt=t.AddHours(1) }]);
        Assert.Same(selected,list[0]); Assert.Equal(0,resets);
    }
    [Fact]
    public async Task BackgroundChunksCannotSelectOrReorderViewedConversation()
    {
        await using var rig=new ConversationWorkspaceRig(); var home=CreateHome(rig);
        var a=await rig.Workspace.NewAsync(false,default); await a.UpdateDraftTextAsync("A question",default);
        var attempt=rig.Ai.QueueAttempt(); await a.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default); await attempt.Started.Task;
        rig.Clock.Advance(TimeSpan.FromMinutes(1));
        var b=await rig.Workspace.NewAsync(false,default); await b.UpdateDraftTextAsync("B draft",default); await b.FlushAsync(default);
        await home.RefreshAsync(default); var selected=home.History.Single(i=>i.Id==b.State.Id);
        for(var i=0;i<20;i++)
        {
            rig.Clock.Advance(TimeSpan.FromSeconds(1)); attempt.Text("part"); await home.RefreshAsync(default);
            Assert.Equal(b.State.Id,home.SelectedConversationId); Assert.Same(selected,home.History[0]);
        }
        attempt.Complete(); await a.WaitForIdleAsync(default);
        await home.SelectAsync(a.State.Id,default); await home.SelectAsync(b.State.Id,default); await home.RefreshAsync(default);
        Assert.Same(selected,home.History[0]); Assert.Equal(b.State.Id,home.SelectedConversationId);
    }
    [Fact]
    public async Task RefreshKeepsActualHistoryListScrollPosition()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig();
            var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            runtime.UpdateDraftTextAsync("current",default).GetAwaiter().GetResult(); runtime.FlushAsync(default).GetAwaiter().GetResult();
            var start=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
            for(var i=0;i<40;i++) rig.Store.SaveAsync(new() { CreatedAt=start.AddMinutes(i),UpdatedAt=start.AddDays(1),Draft=new("draft "+i,[],1) },default).GetAwaiter().GetResult();
            var home=CreateHome(rig); var window=new HomeWindow(home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);
            try
            {
                window.Show(); window.UpdateLayout(); StaTest.Pump();
                var list=Assert.IsType<ListBox>(window.FindName("HistoryList"));
                var scroll=VisualChildren(list).OfType<ScrollViewer>().First(); scroll.ScrollToVerticalOffset(10); window.UpdateLayout(); StaTest.Pump();
                var offset=scroll.VerticalOffset; Assert.True(offset>0); var selected=list.SelectedItem;
                for(var i=0;i<20;i++) { home.RefreshAsync(default).GetAwaiter().GetResult(); window.UpdateLayout(); StaTest.Pump(); }
                Assert.Equal(offset,scroll.VerticalOffset); Assert.Same(selected,list.SelectedItem);
            }
            finally { window.Close(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    private static IEnumerable<DependencyObject> VisualChildren(DependencyObject parent)
    {
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
        {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i); yield return child;
            foreach(var nested in VisualChildren(child)) yield return nested;
        }
    }
    private static HomeViewModel CreateHome(ConversationWorkspaceRig rig)=>new(rig.Workspace,rig.Store,new StubServices(),()=>Task.FromResult(rig.Settings),rig.Verification,new Host(),()=>Task.CompletedTask);
    private sealed class Host:IOverlayHost
    {
        public Task<ProtectionResult> OpenAsync(CancellationToken ct)=>Task.FromResult(new ProtectionResult(true,0,17));
        public void Show() { } public void Close() { }
    }
}
