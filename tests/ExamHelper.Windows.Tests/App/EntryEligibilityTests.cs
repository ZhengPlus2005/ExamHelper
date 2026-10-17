using System.Net;
using System.Net.Http;
using System.Text;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Native;
using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class EntryEligibilityTests
{
    private static readonly ServiceVerification Verification = new(new byte[32]);
    private static AppSettings Verified()
    {
        var settings = new AppSettings { Ai = new() { SecretFingerprint = Verification.SecretDigest("ai") },Asr = new() { SecretFingerprint = Verification.SecretDigest("asr") } };
        return Verification.RecordAsrSuccess(Verification.RecordAiSuccess(settings,Verification.AiFingerprint(settings)),Verification.AsrFingerprint(settings));
    }
    [Fact]
    public async Task UnverifiedCannotStartOrContinue()
    {
        await using var rig = new Rig(new()); await rig.Home.StartAsync(default); await rig.Home.ContinueAsync(Guid.NewGuid(),default);
        Assert.True(rig.Home.HomeVisible); Assert.Equal(0,rig.Host.OpenCalls); Assert.Empty(await rig.Store.ListAsync(default));
    }
    [Fact]
    public async Task ProtectionFailureKeepsHomeVisibleAndDoesNotCreateEmptyHistory()
    {
        await using var rig = new Rig(Verified()); rig.Host.Result = new(false,5,0);
        await rig.Home.StartAsync(default);
        Assert.True(rig.Home.HomeVisible); Assert.Equal(1,rig.Host.OpenCalls); Assert.Equal(0,rig.Host.ShowCalls);
        Assert.Empty(await rig.Store.ListAsync(default)); Assert.NotEmpty(rig.Home.Status);
    }
    [Fact]
    public async Task DeleteRequiresConfirmation()
    {
        await using var rig = new Rig(new()); var session = new SessionSnapshot(); await rig.Store.SaveAsync(session,default);
        await rig.Home.DeleteAsync(session.Id,() => Task.FromResult(false),default);
        Assert.NotNull(await rig.Store.LoadAsync(session.Id,default));
        await rig.Home.DeleteAsync(session.Id,() => Task.FromResult(true),default);
        Assert.Null(await rig.Store.LoadAsync(session.Id,default));
    }
    [Fact]
    public async Task ConfigChangedDuringTestDoesNotPassNewConfig()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHttpHandler((_,_) => { entered.TrySetResult(); return response.Task; });
        using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); var store = new InMemorySettingsStore();
        var services = new StubServices(); await using var recording = new RecordingController(services,new ManualTimeProvider());
        var runner = new ServiceTestRunner(http,secrets,Verification,[1]);
        await using var settings = new SettingsViewModel(new(),"ai","asr",store,secrets,Verification,runner,recording,_ => { });
        var running = settings.RunImageTestAsync(default); await entered.Task;
        settings.AiModel = "changed-while-testing";
        response.SetResult(new(HttpStatusCode.OK) { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"EH7Q9\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") });
        await running;
        Assert.False(Verification.IsAiVerified(settings.BuildCandidate()));
        Assert.Contains("变化",settings.Status);
    }
    private sealed class Rig : IAsyncDisposable
    {
        public InMemorySessionStore Store { get; } = new(); public Host Host { get; } = new();
        public SessionCoordinator Coordinator { get; } public HomeViewModel Home { get; }
        public Rig(AppSettings settings)
        {
            var services = new StubServices();
            Coordinator = new(services,services,services,services,services,() => settings,Store,new ManualTimeProvider());
            Home = new(Coordinator,Store,services,() => Task.FromResult(settings),Verification,Host,() => Task.CompletedTask);
        }
        public ValueTask DisposeAsync() => Coordinator.DisposeAsync();
    }
    private sealed class Host : IOverlayHost
    {
        public int OpenCalls,ShowCalls; public ProtectionResult Result = new(true,0,0x11);
        public Task<ProtectionResult> OpenAsync(CancellationToken ct) { OpenCalls++; return Task.FromResult(Result); }
        public void Show() => ShowCalls++; public void Close() { }
    }
}