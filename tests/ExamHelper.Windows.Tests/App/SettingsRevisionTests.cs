using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class SettingsRevisionTests
{
    [Theory]
    [InlineData(SettingsCloseChoice.Save,true)]
    [InlineData(SettingsCloseChoice.Discard,true)]
    [InlineData(SettingsCloseChoice.Cancel,false)]
    public async Task DirtyCloseRespectsSaveDiscardAndCancel(SettingsCloseChoice choice,bool closes)
    {
        await using var rig = new Rig();
        Assert.False(rig.Vm.HasUnsavedChanges);
        rig.Vm.DirectCapturePrompt = "未防抖保存的内容";
        Assert.True(rig.Vm.HasUnsavedChanges);
        Assert.Equal(closes,await rig.Vm.RequestCloseAsync(() => Task.FromResult(choice),default));
        Assert.Equal(choice == SettingsCloseChoice.Save ? "未防抖保存的内容" : new AppSettings().DirectCapturePrompt,rig.Store.Value.DirectCapturePrompt);
    }
    [Fact]
    public async Task FailedCloseSaveKeepsWindowOpenAndContent()
    {
        await using var rig = new Rig(); rig.Vm.AiModel = "edited"; rig.Store.Fail = true;
        Assert.False(await rig.Vm.RequestCloseAsync(() => Task.FromResult(SettingsCloseChoice.Save),default));
        Assert.Equal("edited",rig.Vm.AiModel); Assert.True(rig.Vm.HasUnsavedChanges); Assert.NotEmpty(rig.Vm.Status);
    }
    [Fact]
    public async Task ProfilesTestWithoutActivationAndKeepIndependentKeysAndEvidence()
    {
        await using var rig = new Rig(); var first = rig.Vm.SelectedAiProfile!;
        var second = rig.Vm.AddAiProfile(); second.Name = "兼容 B"; rig.Vm.AiApiKey = "key-B"; rig.Vm.AiModel = "model-B";
        await rig.Vm.RunImageTestAsync(default);
        var candidate = rig.Vm.BuildCandidate();
        Assert.Equal(first.Id,candidate.ActiveAiProfileId);
        Assert.Equal("model-B",rig.SentModel); Assert.Equal("key-B",rig.SentKey);
        Assert.True(second.IsVerified); Assert.False(first.IsVerified);
        rig.Vm.ActivateSelectedAiProfile(); await rig.Vm.SaveAsync(default);
        Assert.True(rig.Verification.IsAiVerified(rig.Store.Value));
        rig.Vm.SelectedAiProfile = first; Assert.Equal("ai",rig.Vm.AiApiKey);
        rig.Vm.SelectedAiProfile = second; Assert.Equal("key-B",rig.Vm.AiApiKey);
        rig.Vm.DeleteSelectedAiProfile(); await rig.Vm.SaveAsync(default);
        Assert.Null(rig.Store.Value.ActiveAiProfileId); Assert.False(rig.Verification.IsAiVerified(rig.Store.Value));
    }
    [Fact]
    public async Task SwitchingSelectionDuringTestRecordsOnlyOriginalProfile()
    {
        await using var rig = new Rig(); var first = rig.Vm.SelectedAiProfile!;
        var second = rig.Vm.AddAiProfile(); second.ApiKey = "key-B";
        rig.Delay = true; var running = rig.Vm.RunImageTestAsync(default); await rig.Entered.Task;
        rig.Vm.SelectedAiProfile = first; rig.Release.TrySetResult(); await running;
        Assert.True(second.IsVerified); Assert.False(first.IsVerified);
    }
    [Fact]
    public async Task DeletedProfileCannotBeResurrectedByLateTest()
    {
        await using var rig = new Rig(); var second = rig.Vm.AddAiProfile(); second.ApiKey = "key-B";
        rig.Delay = true; var running = rig.Vm.RunImageTestAsync(default); await rig.Entered.Task;
        rig.Vm.DeleteSelectedAiProfile(); await rig.Vm.SaveAsync(default); rig.Release.TrySetResult(); await running;
        Assert.DoesNotContain(rig.Vm.AiProfiles,p => p.Id == second.Id);
        Assert.DoesNotContain(rig.Store.Value.AiProfiles!.Value,p => p.Id == second.Id);
    }
    [Fact]
    public async Task WindowCloseButtonAndXUseTheDirtyClosePolicy()
    {
        await StaTest.RunAsync(() =>
        {
            var rig = new Rig(); var choice = SettingsCloseChoice.Cancel; var prompts = 0;
            var window = new SettingsWindow(rig.Vm,() => { prompts++; return Task.FromResult(choice); });
            window.Show(); StaTest.Pump();
            rig.Vm.DirectCapturePrompt = "刚刚修改";
            window.Close(); StaTest.Pump();
            Assert.True(window.IsVisible); Assert.Equal(1,prompts);
            choice = SettingsCloseChoice.Discard; window.Close(); StaTest.Pump();
            Assert.False(window.IsVisible); Assert.Equal(2,prompts);
            rig.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    [Fact]
    public async Task OpacityPercentageAndPreviewFollowSliderValue()
    {
        await StaTest.RunAsync(() =>
        {
            var rig = new Rig(); var window = new SettingsWindow(rig.Vm,() => Task.FromResult(SettingsCloseChoice.Discard));
            window.Show();
            var tabs = Assert.IsType<System.Windows.Controls.TabControl>(window.FindName("SettingsTabs")); tabs.SelectedIndex = 2;
            rig.Vm.Opacity = 0.55; window.UpdateLayout(); StaTest.Pump();
            var label = Assert.IsType<System.Windows.Controls.TextBlock>(window.FindName("OpacityPercentage"));
            Assert.Contains("55",label.Text);
            var preview = Assert.IsType<ExamHelper.App.Controls.OpacityPreview>(window.FindName("OpacityDemo"));
            Assert.Equal(0.55,preview.BackgroundOpacity);
            SavePreview(window,"opacity55.png");
            rig.Vm.Opacity = 0.35; window.UpdateLayout(); StaTest.Pump(); SavePreview(window,"opacity35.png");
            rig.Vm.Opacity = 0.85; window.UpdateLayout(); StaTest.Pump(); SavePreview(window,"opacity85.png");
            window.Close(); StaTest.Pump(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    [Fact]
    public async Task ProfileListAndMicrophoneMeterRenderWithLiveBindings()
    {
        await StaTest.RunAsync(() =>
        {
            var rig = new Rig(); var backup = rig.Vm.AddAiProfile(); backup.Name = "备用兼容接口"; backup.ApiKey = "fake-backup-key";
            var window = new SettingsWindow(rig.Vm,() => Task.FromResult(SettingsCloseChoice.Discard));
            window.Show(); window.UpdateLayout(); StaTest.Pump(); SavePreview(window,"ai-profiles.png");
            var tabs = Assert.IsType<System.Windows.Controls.TabControl>(window.FindName("SettingsTabs")); tabs.SelectedIndex = 1;
            rig.Vm.ReadLevel = () => 0.1f; rig.Vm.RefreshTelemetry(); window.UpdateLayout(); StaTest.Pump();
            var meter = Assert.IsType<System.Windows.Controls.ProgressBar>(window.FindName("MicrophoneMeter"));
            Assert.InRange(meter.Value,0.65,0.68); SavePreview(window,"microphone-meter.png");
            rig.Vm.ReadLevel = () => 0; rig.Vm.RefreshTelemetry(); StaTest.Pump(); Assert.Equal(0,meter.Value);
            window.Close(); StaTest.Pump(); rig.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    [Fact]
    public async Task ActualProfileSelectionDoesNotOverwriteKeysOrDirtySavedConfiguration()
    {
        await StaTest.RunAsync(() =>
        {
            var rig = new Rig(); var first = rig.Vm.SelectedAiProfile!;
            var second = rig.Vm.AddAiProfile(); second.Name = "B"; second.ApiKey = "key-B"; second.Model = "model-B";
            rig.Vm.SaveAsync(default).GetAwaiter().GetResult(); var prompts = 0;
            var window = new SettingsWindow(rig.Vm,() => { prompts++; return Task.FromResult(SettingsCloseChoice.Discard); });
            window.Show(); StaTest.Pump();
            var list = Assert.IsType<System.Windows.Controls.ListBox>(window.FindName("ProfilesList"));
            list.SelectedItem = first; StaTest.Pump();
            Assert.Equal("ai",Assert.IsType<System.Windows.Controls.PasswordBox>(window.FindName("AiKey")).Password);
            list.SelectedItem = second; StaTest.Pump();
            Assert.Equal("key-B",Assert.IsType<System.Windows.Controls.PasswordBox>(window.FindName("AiKey")).Password);
            Assert.Equal("model-B",rig.Vm.AiModel); Assert.False(rig.Vm.HasUnsavedChanges);
            window.Close(); StaTest.Pump(); Assert.Equal(0,prompts);
            rig.DisposeAsync().AsTask().GetAwaiter().GetResult();
        });
    }
    private static void SavePreview(System.Windows.Window window,string name)
    {
        var directory = Environment.GetEnvironmentVariable("EXAMHELPER_TEST_UI_OUTPUT");
        if(string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,System.Windows.Media.PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
        encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory,name)); encoder.Save(output);
    }
    private sealed class Rig : IAsyncDisposable
    {
        public Store Store { get; } = new(); public ServiceVerification Verification { get; } = new(new byte[32]);
        public SettingsViewModel Vm { get; } public bool Delay; public string? SentModel,SentKey;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HttpClient _http;
        public Rig()
        {
            _http = new(new FakeHttpHandler(async (request,ct) =>
            {
                SentKey = request.Headers.Authorization?.Parameter;
                var json = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                SentModel = json.RootElement.GetProperty("model").GetString(); Entered.TrySetResult();
                if(Delay) await Release.Task.WaitAsync(ct);
                return new(HttpStatusCode.OK) { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"EH7Q9\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") };
            }));
            var secrets = new InMemorySecretStore(); var recording = new RecordingController(new StubServices(),new ManualTimeProvider());
            Vm = new(new(),"ai","asr",Store,secrets,Verification,new(_http,secrets,Verification,[1]),recording,_ => { });
        }
        public async ValueTask DisposeAsync() { await Vm.DisposeAsync(); _http.Dispose(); }
    }
    private sealed class Store : ISettingsStore
    {
        public AppSettings Value = new(); public bool Fail;
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
        public Task SaveAsync(AppSettings s,CancellationToken ct) { if(Fail) throw new IOException("save"); Value = s; return Task.CompletedTask; }
    }
}
