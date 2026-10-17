using System.Windows.Interop;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class OverlayLifecycleTests
{
    [Fact]
    public async Task HideKeepsRequestsAndHotkeys()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig = new Rig(); rig.Start("问题");
            var attempt = rig.Ai.QueueAttempt();
            rig.Coordinator.ExecuteAsync(AppCommand.Send,default).GetAwaiter().GetResult();
            attempt.Started.Task.GetAwaiter().GetResult();
            rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            var count = rig.Keys.Registered.Count;
            rig.Coordinator.ExecuteAsync(AppCommand.ToggleVisibility,default).GetAwaiter().GetResult(); StaTest.Pump();
            Assert.False(rig.Host.CurrentWindow!.IsVisible); Assert.Equal(count,rig.Keys.Registered.Count);
            Assert.Equal(AnswerState.Generating,rig.Coordinator.State.AnswerState);
            rig.Coordinator.ExecuteAsync(AppCommand.ToggleVisibility,default).GetAwaiter().GetResult(); StaTest.Pump();
            Assert.True(rig.Host.CurrentWindow.IsVisible);
            attempt.Text("答案"); attempt.Complete(); rig.Coordinator.WaitForIdleAsync(default).GetAwaiter().GetResult();
        });
    }
    [Fact]
    public async Task ReturnClearsDraftAndUnregisters()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig = new Rig(); rig.Start("返回清除");
            rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            rig.Coordinator.ReturnHomeAsync(default).GetAwaiter().GetResult(); StaTest.Pump();
            Assert.Empty(rig.Keys.Registered); Assert.Null(rig.Host.CurrentWindow);
            Assert.False(rig.Coordinator.State.Draft.HasContent); Assert.Equal(1,rig.HomeCalls);
        });
    }
    [Fact]
    public async Task ExitSavesDraft()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig = new Rig(); rig.Start("退出保留"); var id = rig.Coordinator.State.Id;
            rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            rig.Coordinator.ExitAsync(default).GetAwaiter().GetResult(); StaTest.Pump();
            Assert.Equal("退出保留",rig.Store.LoadAsync(id,default).GetAwaiter().GetResult()!.Draft.Text);
            Assert.Empty(rig.Keys.Registered); Assert.Equal(1,rig.ExitCalls);
        });
    }
    [Fact]
    public async Task RecreatedHandleMustReapplyProtectionAndDraftNeverAcceptsKeyboardFocus()
    {
        await StaTest.RunAsync(() =>
        {
            using var rig = new Rig(); rig.Start("只读");
            rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            var window = rig.Host.CurrentWindow!; var first = rig.Protection.Sets;
            Assert.False(window.ShowActivated);
            var preview = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("DraftPreview"));
            Assert.False(preview.Focusable); Assert.False(preview.Focus());
            Assert.Null(window.FindName("SettingsButton"));
            rig.Host.Close(); rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            Assert.True(rig.Protection.Sets > first); Assert.True(rig.Host.CurrentWindow!.Protection.Success);
        });
    }
    [Theory]
    [InlineData(480,360)]
    [InlineData(640,720)]
    public async Task ManyImagesKeepActionsVisibleAndLastImageReachable(double width,double height)
    {
        await StaTest.RunAsync(() =>
        {
            using var rig = new Rig(); rig.Start("语音草稿");
            for(int i = 0;i < 40;i++) rig.Coordinator.ExecuteAsync(AppCommand.CaptureToDraft,default).GetAwaiter().GetResult();
            rig.Host.OpenAsync(default).GetAwaiter().GetResult(); rig.Host.Show(); StaTest.Pump();
            var window = rig.Host.CurrentWindow!; window.Width = width; window.Height = height; window.UpdateLayout(); StaTest.Pump();
            foreach(var name in new[] { "SendButton","ClearButton" })
            {
                var button = Assert.IsType<System.Windows.Controls.Button>(window.FindName(name));
                var bounds = button.TransformToAncestor(window).TransformBounds(new System.Windows.Rect(button.RenderSize));
                Assert.InRange(bounds.Bottom,1,window.ActualHeight);
            }
            var strip = Descendants(window).OfType<ExamHelper.App.Controls.ThumbnailStrip>().Single();
            var scroller = Descendants(strip).OfType<System.Windows.Controls.ScrollViewer>().First();
            Assert.True(scroller.ScrollableHeight > 0);
            scroller.ScrollToEnd();
            var content = Assert.IsType<System.Windows.Controls.ScrollViewer>(window.FindName("AnswerScroll"));
            content.ScrollToEnd(); window.UpdateLayout(); StaTest.Pump();
            var items = Assert.IsType<System.Windows.Controls.ItemsControl>(strip.FindName("Items"));
            var last = Assert.IsAssignableFrom<System.Windows.FrameworkElement>(items.ItemContainerGenerator.ContainerFromIndex(39));
            var lastBounds = last.TransformToAncestor(window).TransformBounds(new System.Windows.Rect(last.RenderSize));
            Assert.InRange(lastBounds.Top,0,window.ActualHeight);
            Assert.InRange(lastBounds.Bottom,1,window.ActualHeight);
            var bar = Assert.IsType<System.Windows.Controls.Primitives.ScrollBar>(scroller.Template.FindName("PART_VerticalScrollBar",scroller));
            var track = Assert.IsType<System.Windows.Controls.Primitives.Track>(bar.Template.FindName("PART_Track",bar));
            Assert.NotNull(ExamHelper.App.Controls.DwellBehavior.GetCommand(track.Thumb));
        });
    }
    private static IEnumerable<System.Windows.DependencyObject> Descendants(System.Windows.DependencyObject root)
    {
        for(int i = 0;i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root,i); yield return child;
            foreach(var descendant in Descendants(child)) yield return descendant;
        }
    }
    private sealed class Rig : IDisposable
    {
        public DeferredAiClient Ai { get; } = new(); public InMemorySessionStore Store { get; } = new();
        public Keys Keys { get; } = new(); public Protection Protection { get; } = new();
        public SessionCoordinator Coordinator { get; } public OverlayHost Host { get; }
        public int HomeCalls,ExitCalls;
        public Rig()
        {
            var services = new StubServices(); var settings = new AppSettings();
            Coordinator = new(Ai,services,services,services,services,() => settings,Store,new ManualTimeProvider());
            Host = new(Coordinator,services,() => settings,_ => Task.CompletedTask,
                () => { HomeCalls++; return Task.CompletedTask; },() => { ExitCalls++; return Task.CompletedTask; },Protection,Keys);
        }
        public void Start(string text)
        {
            Coordinator.StartNewAsync(default).GetAwaiter().GetResult();
            Coordinator.AppendSpeechResultAsync(new(Coordinator.State.Id,Coordinator.State.SpeechGeneration,Guid.NewGuid(),text),default).GetAwaiter().GetResult();
        }
        public void Dispose() { Host.Close(); Coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
    }
    private sealed class Protection : IWindowProtectionApi
    {
        public int Sets;
        public bool Set(nint h,uint a,out int e) { Sets++;e = 0;return true; }
        public bool Get(nint h,out uint a,out int e) {a = 0x11;e = 0;return true;}
    }
    private sealed class Keys : IHotkeyApi
    {
        public HashSet<(nint,int)> Registered { get; } = [];
        public bool Register(nint h,int id,uint mods,uint key,out int error) {error = 0;return Registered.Add((h,id));}
        public bool Unregister(nint h,int id) => Registered.Remove((h,id));
    }
}