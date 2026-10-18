using System.Windows;
using System.Windows.Controls;
using ExamHelper.App.Controls;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Sessions;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class AuxiliaryLayout04Tests
{
    [Theory]
    [InlineData(480,360)]
    [InlineData(560,500)]
    [InlineData(640,720)]
    public async Task ThreeMainRowsFillTheWindowAndActionsRemainReachable(double width,double height)
    {
        await StaTest.RunAsync(()=>
        {
            var rig=new ConversationWorkspaceRig();var coordinator=rig.Workspace.NewAsync(true,default).GetAwaiter().GetResult();var view=new OverlayViewModel(coordinator,new StubServices(),verification:rig.Verification);
            var window=new OverlayWindow(view,rig.Settings,new Protection(),new Keys()) { Width=width,Height=height };
            try
            {
                window.Show();window.UpdateLayout();StaTest.Pump();
                CheckRow(window,new[] { "NewButton","StopButton","FoldButton","HomeButton","HideButton","HintButton" });
                CheckRow(window,new[] { "DirectButton","CaptureButton","RecordButton","ClearButton","SendButton" });
                var selector=Assert.IsType<ModelSelector>(window.FindName("AuxiliaryModels"));CheckRow(selector,new[] { "ProfileEntry","ModelEntry","ThinkingEntry" });
                var send=Assert.IsType<Button>(window.FindName("SendButton"));Assert.InRange(send.TranslatePoint(new Point(0,send.ActualHeight),window).Y,1,height-10);
                Assert.Equal(1,DwellBehavior.GetDelay(send));Assert.Equal(2,DwellBehavior.GetDelay(Assert.IsType<Button>(window.FindName("HomeButton"))));
                coordinator.AppendImagesAsync([new(Guid.NewGuid(),"owned-test.png",2560,1440)],default).GetAwaiter().GetResult();coordinator.UpdateDraftTextAsync("question",default).GetAwaiter().GetResult();var attempt=rig.Ai.QueueAttempt();coordinator.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default).GetAwaiter().GetResult();attempt.Started.Task.GetAwaiter().GetResult();
                attempt.Text("```cpp\nint x;\n```");Wait(()=>coordinator.State.Messages.Last().Text.Length>0);view.Refresh();window.UpdateLayout();StaTest.Pump();var message=view.Messages.Last();rig.Clock.Advance(TimeSpan.FromSeconds(12.4));view.Refresh();Assert.Same(message,view.Messages.Last());Assert.Equal("已用 12 秒",message.TimingText);
                window.Hide();rig.Clock.Advance(TimeSpan.FromSeconds(3));window.Show();view.Refresh();Assert.Equal("已用 15 秒",message.TimingText);
                attempt.Fail(new ExamHelper.Core.Common.ServiceFailure(ExamHelper.Core.Common.FailureCategory.Transport,"owned test failure"));coordinator.WaitForIdleAsync(default).GetAwaiter().GetResult();view.Refresh();window.UpdateLayout();StaTest.Pump();Assert.Equal("用时 15.4 秒",message.TimingText);Assert.True(Assert.IsType<Button>(window.FindName("RetryRequestButton")).IsVisible);
                view.ReportError(new string('错',300));window.UpdateLayout();Assert.InRange(send.TranslatePoint(new Point(0,send.ActualHeight),window).Y,1,height-10);
                foreach(var button in MarkdownViewTests.Nodes(window).OfType<Button>().Where(button=>button.IsVisible && DwellBehavior.GetCommand(button) is not null))Assert.IsType<DwellOutline>(button.Template.FindName("Outline",button));
                Assert.Empty(MarkdownViewTests.Nodes(window).OfType<ProgressBar>());Assert.Contains(MarkdownViewTests.Nodes(window).OfType<HoverDeleteRing>(),_=>true);
                Assert.DoesNotContain(MarkdownViewTests.Nodes(window).OfType<TextBlock>(),text=>text.Text.Contains("只读") || text.Text.Contains("移开“提示”"));

            }
            finally { window.CloseFromHost();rig.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    [Fact] public async Task LongAndIncompleteProfilesKeepDisabledChoicesVisibleInSameWindow()
    {
        await StaTest.RunAsync(()=>
        {
            var proof=new ExamHelper.Core.Settings.ServiceVerification(new byte[32]);var settings=TestSettings.Qualified(proof);var profile=settings.AiProfiles!.Value[0];var original=profile.Models!.Value[0];
            profile=profile with { Models=profile.Models.Value.AddRange(Enumerable.Range(0,32).Select(i=>new ExamHelper.Core.Settings.AiModelProfile { Name="unverified-model-with-a-long-name-"+i })) };settings=settings with { AiProfiles=[profile,new() { Name="" }] };
            var selector=new ModelSelector { AuxiliaryInteraction=true };selector.Configure(settings,ExamHelper.Core.Settings.ModelCatalog.Select(profile,original),true,proof);var window=new Window { Content=selector,Width=480,Height=360 };
            try
            {
                window.Show();window.UpdateLayout();StaTest.Pump();selector.OpenHoverMenu(ModelSelectorPart.Model);window.UpdateLayout();StaTest.Pump();var buttons=MarkdownViewTests.Nodes(selector).OfType<Button>().Where(button=>button.Tag is ModelChoice).ToArray();Assert.Equal(33,buttons.Length);
                foreach(var button in buttons)
                { Assert.IsType<DwellOutline>(button.Template.FindName("Outline",button));Assert.Same(System.Windows.PresentationSource.FromVisual(selector),System.Windows.PresentationSource.FromVisual(button));if(button.Tag is ModelChoice { IsEnabled:false }) { Assert.False(button.IsEnabled);Assert.False(DwellBehavior.GetCommand(button)!.CanExecute(null));Assert.Equal(0,DwellBehavior.GetProgress(button)); } }
                var scroll=Assert.IsType<ScrollViewer>(selector.FindName("MenuScroll"));Assert.True(scroll.ScrollableHeight>0);Assert.InRange(scroll.ActualHeight,1,160);
                selector.OpenHoverMenu(ModelSelectorPart.Profile);window.UpdateLayout();StaTest.Pump();Assert.Contains(MarkdownViewTests.Nodes(selector).OfType<Button>(),button=>button.Tag is ExamHelper.Core.Settings.AiServiceProfile p && p.Name=="");
                selector.SetHoverSuspended(true);Assert.False(selector.IsMenuOpen);selector.OpenHoverMenu(ModelSelectorPart.Model);Assert.False(selector.IsMenuOpen);
            }
            finally { window.Close();StaTest.Pump(); }
        });
    }
    private static void Wait(Func<bool> ready) { var watch=System.Diagnostics.Stopwatch.StartNew();while(!ready() && watch.Elapsed<TimeSpan.FromSeconds(3)) { StaTest.Pump();Thread.Sleep(3); }Assert.True(ready()); }
    private static void CheckRow(FrameworkElement root,string[] names)
    {
        var buttons=names.Select(name=>Assert.IsType<Button>(root.FindName(name))).ToArray();var row=Assert.IsAssignableFrom<Panel>(buttons[0].Parent);
        Assert.All(buttons,button=>Assert.InRange(button.ActualWidth,buttons[0].ActualWidth-0.01,buttons[0].ActualWidth+0.01));
        var last=buttons[^1];Assert.InRange(last.TranslatePoint(new Point(last.ActualWidth,0),row).X+last.Margin.Right,row.ActualWidth-0.01,row.ActualWidth+0.01);
        Assert.All(buttons,button=>Assert.InRange(button.TranslatePoint(new Point(),row).Y,0,row.ActualHeight-button.ActualHeight));
    }
    internal sealed class Protection:IWindowProtectionApi
    { public bool Set(nint h,uint a,out int e) { e=0;return true; }public bool Get(nint h,out uint a,out int e) { a=17;e=0;return true; } }
    internal sealed class Keys:IHotkeyApi
    { public bool Register(nint h,int i,uint m,uint k,out int e) { e=0;return true; }public bool Unregister(nint h,int i)=>true; }
}
