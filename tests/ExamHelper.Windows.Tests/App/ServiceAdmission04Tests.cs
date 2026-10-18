using System.Net;
using System.Net.Http;
using System.Text;
using ExamHelper.App.Controls;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Windows.Settings;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class ServiceAdmission04Tests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EmptyOrIncompleteServicesDoNotBlockProtectedEntry(bool incomplete)
    {
        var settings = new AppSettings();
        if(incomplete) settings = settings with { AiProfiles = [new() { BaseAddress="https://" }] };
        var store = new InMemorySessionStore(); var stub = new StubServices(); var proof = new ServiceVerification(new byte[32]); var host = new Host();
        await using var workspace = new ConversationWorkspace(store,() => new SessionCoordinator(stub,stub,stub,stub,stub,() => settings,store,new ManualTimeProvider(),verification:proof));
        var home = new HomeViewModel(workspace,store,stub,() => Task.FromResult(settings),proof,host,() => Task.CompletedTask);
        await home.StartAsync(default);
        Assert.False(home.HomeVisible);
        Assert.Equal(1,host.OpenCalls);
        Assert.True(workspace.Selected!.CanExecute(ExamHelper.Core.Interaction.AppCommand.CaptureToDraft));
        Assert.False(workspace.Selected.CanExecute(ExamHelper.Core.Interaction.AppCommand.ToggleRecording));
    }
    [Fact]
    public async Task TextAndImageTestsPersistIndependentlyAcrossSecretRebase()
    {
        await using var rig = new SettingsRig();
        await rig.Vm.RunTextTestAsync(default);
        var textOnly = rig.Store.Value.AiProfiles!.Value.Single().Models!.Value.Single();
        Assert.NotNull(textOnly.TextVerifiedFingerprint); Assert.Null(textOnly.VerifiedFingerprint);
        await rig.Vm.RunImageTestAsync(default);
        Assert.True(rig.Proof.CanUseInAuxiliary(rig.Store.Value,ModelCatalog.Default(rig.Store.Value)),rig.Vm.Status);
        Assert.NotEqual("ai",rig.Store.Value.Ai.SecretId);
        rig.Answer = "wrong-marker"; await rig.Vm.RunImageTestAsync(default);
        var failedImage = rig.Store.Value.AiProfiles!.Value.Single().Models!.Value.Single();
        Assert.NotNull(failedImage.TextVerifiedFingerprint); Assert.Null(failedImage.VerifiedFingerprint);
        rig.Answer = "EH7Q9"; await rig.Vm.RunImageTestAsync(default);
        rig.Answer = ""; await rig.Vm.RunTextTestAsync(default);
        var failedText = rig.Store.Value.AiProfiles!.Value.Single().Models!.Value.Single();
        Assert.Null(failedText.TextVerifiedFingerprint); Assert.NotNull(failedText.VerifiedFingerprint);
    }
    [Fact]
    public async Task BusyTestDoesNotRevokeAlreadyVerifiedModel()
    {
        await using var rig = new SettingsRig(); await rig.Vm.RunTextTestAsync(default); await rig.Vm.RunImageTestAsync(default);
        using var running = rig.Generation.TryAcquire(Guid.NewGuid());
        await rig.Vm.RunImageTestAsync(default);
        Assert.True(rig.Proof.CanUseInAuxiliary(rig.Store.Value,ModelCatalog.Default(rig.Store.Value)),rig.Vm.Status);
    }
    [Fact]
    public async Task TextResultForAnEditedKeyCannotQualifyNewConfiguration()
    {
        await using var rig = new SettingsRig(); rig.Delay = true;
        var pending = rig.Vm.RunTextTestAsync(default); await rig.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        rig.Vm.AiApiKey = "new-key"; rig.Release.TrySetResult(); await pending;
        Assert.All(rig.Store.Value.AiProfiles!.Value[0].Models!.Value,m => Assert.Null(m.TextVerifiedFingerprint));
        Assert.Contains("变化",rig.Vm.Status);
    }
    [Fact]
    public void ModelRenameClearsOnlyItsOwnProofs()
    {
        var editor = new ModelEditor(new() { Name="old",TextVerifiedFingerprint="text-proof",VerifiedFingerprint="image-proof" });
        editor.Name = "new";
        Assert.Null(editor.Build().TextVerifiedFingerprint);
        Assert.Null(editor.Build().VerifiedFingerprint);
    }
    [Fact]
    public void ProfileAddressAndKeyChangesClearAllModelsButThinkingDoesNot()
    {
        var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Qualified(proof); var p = settings.AiProfiles!.Value[0];
        var extra = new AiModelProfile { Name="kimi-k3",TextVerifiedFingerprint="text",VerifiedFingerprint="image" };
        var editor = new AiProfileEditor(p with { Models=p.Models!.Value.Add(extra) },"ai",proof);
        var second = editor.Models.Single(m => m.Name=="kimi-k3");
        second.DefaultThinking = new(ThinkingKind.Effort,"high");
        Assert.Equal("text",second.Build().TextVerifiedFingerprint); Assert.Equal("image",second.Build().VerifiedFingerprint);
        editor.BaseAddress = "https://changed.invalid/v1";
        Assert.All(editor.Models,m => { Assert.Null(m.Build().TextVerifiedFingerprint); Assert.Null(m.Build().VerifiedFingerprint); });
        second.TextVerifiedFingerprint="text-again"; second.VerifiedFingerprint="image-again";
        editor.ApiKey = "new-key";
        Assert.All(editor.Models,m => { Assert.Null(m.Build().TextVerifiedFingerprint); Assert.Null(m.Build().VerifiedFingerprint); });
    }
    [Fact]
    public async Task UnverifiedModelsRemainVisibleAndDisabledInProtectedMenu()
    {
        await StaTest.RunAsync(() =>
        {
            var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Configured();
            var p = settings.AiProfiles!.Value[0]; var a = p.Models!.Value[0]; var b = new AiModelProfile { Name="untested" };
            settings = settings with { AiProfiles=[p with { Models=[a,b] }] };
            var fp = proof.AiFingerprint(settings);
            settings = proof.RecordModelResult(settings,p.Id,a.Id,AiTestKind.Text,true,fp);
            settings = proof.RecordModelResult(settings,p.Id,a.Id,AiTestKind.Image,true,fp);
            var selector = new ModelSelector { AuxiliaryInteraction=true };
            selector.Configure(settings,ModelCatalog.Select(p,a),true,proof);
            Assert.Equal(2,selector.ModelChoices.Count);
            selector.OpenHoverMenu(ModelSelectorPart.Model);
            var items = Assert.IsType<System.Windows.Controls.StackPanel>(selector.FindName("MenuItems"));
            var buttons = items.Children.OfType<System.Windows.Controls.Button>().ToArray();
            Assert.Equal(2,buttons.Length);
            Assert.True(buttons.Single(b => ((System.Windows.Controls.TextBlock)b.Content).Text=="model-a").IsEnabled);
            Assert.False(buttons.Single(b => ((System.Windows.Controls.TextBlock)b.Content).Text=="untested").IsEnabled);
        });
    }
    private sealed class SettingsRig : IAsyncDisposable
    {
        public ServiceVerification Proof { get; } = new(new byte[32]);
        public InMemorySettingsStore Store { get; } = new(TestSettings.Configured());
        public ExamHelper.Core.Sessions.AiGenerationGate Generation { get; } = new();
        public SettingsViewModel Vm { get; }
        public string Answer = "EH7Q9"; public bool Delay;
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly HttpClient _http;
        public SettingsRig()
        {
            _http = new(new FakeHttpHandler(async (_,ct) =>
            {
                Entered.TrySetResult(); if(Delay) await Release.Task.WaitAsync(ct);
                return new(HttpStatusCode.OK) { Content=new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\""+Answer+"\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") };
            }));
            var secrets = new InMemorySecretStore();
            Vm = new(Store.Value,"test-key","asr",Store,secrets,Proof,new(_http,secrets,Proof,[1],Generation),new RecordingController(new StubServices(),new ManualTimeProvider()),_ => { });
        }
        public async ValueTask DisposeAsync() { await Vm.DisposeAsync(); _http.Dispose(); }
    }
    private sealed class Host : IOverlayHost
    {
        public int OpenCalls;
        public Task<ProtectionResult> OpenAsync(CancellationToken ct) { OpenCalls++; return Task.FromResult(new ProtectionResult(true,0,17)); }
        public void Show() { }
        public void Close() { }
    }
}
