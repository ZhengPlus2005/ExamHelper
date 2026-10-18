using System.IO;
using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.TestSupport;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class HistorySelection04Tests
{
    [Fact]
    public async Task MainHistoryOffersBatchEditingInsteadOfToolbarRenameAndDelete()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new ConversationWorkspaceRig(); var runtime=rig.Workspace.NewAsync(false,default).GetAwaiter().GetResult();
            runtime.UpdateDraftTextAsync("A",default).GetAwaiter().GetResult(); runtime.FlushAsync(default).GetAwaiter().GetResult();
            var home=new HomeViewModel(rig.Workspace,rig.Store,new StubServices(),()=>Task.FromResult(rig.Settings),rig.Verification,new Host(),()=>Task.CompletedTask);
            var window=new HomeWindow(home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);
            try
            {
                window.Show(); window.UpdateLayout(); StaTest.Pump(); var buttons=Nodes(window).OfType<Button>().ToArray();
                Assert.Single(buttons,b=>Equals(b.Content,"批量编辑"));
                Assert.DoesNotContain(buttons,b=>Equals(b.Content,"改标题"));
            }
            finally { window.Close(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact]
    public async Task DeleteConfirmsDistinctCountOnceAndSelectsLatestOrAnUnsavedBlank()
    {
        await using var rig=new HomeRig(); var a=await rig.New("A"); var b=await rig.New("B"); var c=await rig.New("C");
        await rig.Home.SelectAsync(a.State.Id,default); var prompts=0;
        var result=await rig.Home.DeleteManyAsync([a.State.Id,a.State.Id],count=> { Assert.Equal(1,count);prompts++;return Task.FromResult(true); },default);
        Assert.Single(result.DeletedIds); Assert.Equal(1,prompts); Assert.Equal(c.State.Id,rig.Home.SelectedConversationId);
        await rig.Home.DeleteManyAsync([b.State.Id,c.State.Id],count=> { Assert.Equal(2,count);return Task.FromResult(true); },default);
        Assert.Empty(await rig.Store.ListAsync(default)); Assert.NotNull(rig.Workspace.Selected); Assert.False(rig.Workspace.Selected!.State.Draft.HasContent); Assert.Empty(rig.Workspace.Selected.State.Messages);
    }
    [Fact]
    public async Task FailedCurrentItemRemainsCheckedAndEditableAfterRefresh()
    {
        await using var rig=new HomeRig(); var a=await rig.New("A"); var b=await rig.New("B"); await rig.Home.SelectAsync(b.State.Id,default);
        rig.Home.HistorySelection.Enter(); rig.Home.HistorySelection.SetAll(true); rig.Store.FailId=b.State.Id;
        var result=await rig.Home.DeleteManyAsync([a.State.Id,b.State.Id],_=>Task.FromResult(true),default);
        Assert.Contains(b.State.Id,result.Failures.Keys); Assert.Same(b,rig.Workspace.Selected); Assert.True(rig.Home.History.Single(i=>i.Id==b.State.Id).IsChecked);
        await b.UpdateDraftTextAsync("still editable",default); await b.FlushAsync(default); await rig.Home.RefreshAsync(default);
        Assert.True(rig.Home.History.Single(i=>i.Id==b.State.Id).IsChecked); Assert.Equal("still editable",(await rig.Store.LoadAsync(b.State.Id,default))!.Draft.Text);
    }
    [Fact]
    public void SelectionUsesStableIdsAcrossSortingAndSupportsAllRows()
    {
        var t=DateTimeOffset.Parse("2026-10-18T00:00:00Z");
        var summaries=Enumerable.Range(0,30).Select(i=>new SessionSummary(Guid.NewGuid(),"item "+i,t,true) { CreatedAt=t,SortAt=t.AddMinutes(i) }).ToArray();
        var items=new HistoryCollection();items.Apply(summaries);var selection=new HistorySelectionController(items);selection.Enter();selection.SetAll(true);
        Assert.Equal(30,selection.SelectedIds.Count); var id=items[10].Id; selection.ToggleRow(id); Assert.Equal(29,selection.SelectedIds.Count);
        items.Apply(summaries.Select(s=>s with { SortAt=s.Id==id ? t.AddDays(1) : s.SortAt }).Reverse().ToArray());
        Assert.False(items.Single(i=>i.Id==id).IsChecked); Assert.Equal(29,selection.SelectedIds.Count); selection.Complete(); Assert.False(selection.IsBatchMode); Assert.Empty(selection.SelectedIds);
    }
    [Fact]
    public async Task RenameByRowIdDoesNotChangeCurrentConversation()
    {
        await using var rig=new HomeRig(); var a=new SessionSnapshot { Draft=new("unloaded A",[],1) };await rig.Store.SaveAsync(a,default);var b=await rig.New("B");
        await rig.Home.RenameAsync(a.Id,"renamed A",default);
        Assert.Equal(b.State.Id,rig.Home.SelectedConversationId); Assert.Equal("renamed A",(await rig.Store.LoadAsync(a.Id,default))!.CustomTitle); Assert.Null(b.State.CustomTitle);
    }
    [Fact]
    public async Task CleanupFailureIsSeparateFromSuccessfulHistoryDeletionAndCanBeRetried()
    {
        var fails=true;await using var rig=new HomeRig(()=>fails ? Task.FromException(new IOException("cleanup failure")) : Task.CompletedTask);
        var a=await rig.New("A"); var result=await rig.Home.DeleteManyAsync([a.State.Id],_=>Task.FromResult(true),default);
        Assert.Contains(a.State.Id,result.DeletedIds);Assert.Empty(result.Failures);Assert.Null(await rig.Store.LoadAsync(a.State.Id,default));Assert.True(rig.Home.HasImageCleanupFailure);
        fails=false;await rig.Home.RetryImageCleanupAsync(default);Assert.False(rig.Home.HasImageCleanupFailure);Assert.Empty(rig.Home.History);
    }
    [Fact]
    public async Task ActualRowClickTogglesAndPreviewsButCheckboxOnlyToggles()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new HomeRig(); var a=rig.New("A").GetAwaiter().GetResult(); var b=rig.New("B").GetAwaiter().GetResult();
            var window=new HomeWindow(rig.Home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);
            try
            {
                window.Show();window.UpdateLayout();StaTest.Pump();
                var edit=Assert.IsType<Button>(window.FindName("BatchEditButton")); edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();StaTest.Pump();
                var list=Assert.IsType<ListBox>(window.FindName("HistoryList"));var item=rig.Home.History.Single(i=>i.Id==a.State.Id);
                list.ScrollIntoView(item);window.UpdateLayout();StaTest.Pump();
                var container=Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromItem(item));
                var check=Assert.Single(Nodes(container).OfType<CheckBox>()); Assert.False(check.Focusable);
                var toggle=(System.Windows.Automation.Provider.IToggleProvider)new System.Windows.Automation.Peers.CheckBoxAutomationPeer(check).GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle)!;
                toggle.Toggle();StaTest.Pump();Assert.True(item.IsChecked);Assert.Equal(b.State.Id,rig.Home.SelectedConversationId);
                var row=Nodes(container).OfType<Grid>().Single(g=>g.Name=="HistoryRow");
                void ClickRow()=>list.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,System.Windows.Input.MouseButton.Left) { RoutedEvent=UIElement.PreviewMouseLeftButtonDownEvent,Source=row });
                ClickRow();StaTest.Pump();Assert.False(item.IsChecked);Assert.Equal(a.State.Id,rig.Home.SelectedConversationId);
                ClickRow();StaTest.Pump();Assert.True(item.IsChecked);Assert.Equal(a.State.Id,rig.Home.SelectedConversationId);
                Assert.Contains(Nodes(container).OfType<Button>(),button=>button.Name=="RenameHistoryIcon" && button.Content is TextBlock);
                Assert.Contains(Nodes(container).OfType<Button>(),button=>button.Name=="DeleteHistoryIcon" && button.Content is TextBlock);
            }
            finally { window.Close();rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact]
    public async Task PendingConfirmationPreventsDuplicateSubmissionAndCancelPreservesSelection()
    {
        await using var rig=new HomeRig();var a=await rig.New("A");rig.Home.HistorySelection.Enter();rig.Home.HistorySelection.SetAll(true);
        var answer=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var prompts=0;
        var first=rig.Home.DeleteManyAsync([a.State.Id],_=> { prompts++;return answer.Task; },default);
        var duplicate=await rig.Home.DeleteManyAsync([a.State.Id],_=> { prompts++;return Task.FromResult(true); },default);
        Assert.Empty(duplicate.DeletedIds);Assert.Equal(1,prompts);answer.TrySetResult(false);await first;
        Assert.NotNull(await rig.Store.LoadAsync(a.State.Id,default));Assert.Contains(a.State.Id,rig.Home.HistorySelection.SelectedIds);Assert.True(rig.Home.CanEditHistory);
    }
    [Fact]
    public async Task PendingDeletionDisablesEditingOfTheDraftBeingDeleted()
    {
        await StaTest.RunAsync(() =>
        {
            var rig=new HomeRig();var a=rig.New("A").GetAwaiter().GetResult();var answer=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window=new HomeWindow(rig.Home,()=>throw new NotSupportedException(),()=>Task.CompletedTask);Task? deleting=null;
            try
            {
                window.Show();window.UpdateLayout();StaTest.Pump();
                deleting=rig.Home.DeleteManyAsync([a.State.Id],_=>answer.Task,default);StaTest.Pump();
                Assert.False(Assert.IsType<TextBox>(window.FindName("HomeDraftInput")).IsEnabled);
            }
            finally { answer.TrySetResult(false);deleting?.GetAwaiter().GetResult();window.Close();rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    private sealed class HomeRig:IAsyncDisposable
    {
        public Store Store { get; }=new();public ConversationWorkspace Workspace { get; }public HomeViewModel Home { get; }private readonly ManualTimeProvider _clock=new();
        public HomeRig(Func<Task>? cleanup=null)
        {
            var proof=new ServiceVerification(new byte[32]);var settings=TestSettings.Qualified(proof);var stub=new StubServices();
            Workspace=new(Store,()=>new SessionCoordinator(stub,stub,stub,stub,stub,()=>settings,Store,_clock,verification:proof));
            Home=new(Workspace,Store,stub,()=>Task.FromResult(settings),proof,new Host(),cleanup ?? (()=>Task.CompletedTask));
        }
        public async Task<SessionCoordinator> New(string text)
        { var runtime=await Workspace.NewAsync(false,default);await runtime.UpdateDraftTextAsync(text,default);await runtime.FlushAsync(default);_clock.Advance(TimeSpan.FromMinutes(1));await Home.RefreshAsync(default);return runtime; }
        public ValueTask DisposeAsync()=>Workspace.DisposeAsync();
    }
    private sealed class Store:ISessionStore
    {
        private readonly InMemorySessionStore _inner=new();public Guid? FailId;
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct)=>_inner.SaveAsync(s,ct);public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct)=>_inner.LoadAsync(id,ct);public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct)=>_inner.ListAsync(ct);
        public Task DeleteAsync(Guid id,CancellationToken ct)=>FailId==id ? Task.FromException(new IOException("cannot delete")) : _inner.DeleteAsync(id,ct);
    }
    private static IEnumerable<DependencyObject> Nodes(DependencyObject parent)
    {
        for(var i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++)
        { var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);yield return child;foreach(var nested in Nodes(child)) yield return nested; }
    }
    private sealed class Host:IOverlayHost
    {
        public Task<ProtectionResult> OpenAsync(CancellationToken ct)=>Task.FromResult(new ProtectionResult(true,0,17));
        public void Show() { } public void Close() { }
    }
}
