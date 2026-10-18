using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
public class ProductWindowTests
{
    [Fact]
    public async Task ProductWindowsLoadWithoutResettingStoredKeysOrGrantingEligibility()
    {
        await StaTest.RunAsync(() =>
        {
            var stub = new StubServices(); var verification = new ServiceVerification(new byte[32]);
            var settings = TestSettings.Configured(); var sessions = new InMemorySessionStore();
            var coordinator = new SessionCoordinator(stub,stub,stub,stub,stub,() => settings,sessions,new ManualTimeProvider());
            var home = new HomeViewModel(new ConversationWorkspace(sessions,() => coordinator),sessions,stub,() => Task.FromResult(settings),verification,new Host(),() => Task.CompletedTask);
            using var http = new HttpClient(new FakeHttpHandler((_,_) => throw new InvalidOperationException("UI smoke must not call services")));
            var secrets = new InMemorySecretStore();
            var recording = new RecordingController(stub,new ManualTimeProvider());
            var vm = new SettingsViewModel(settings,"sample-ai-key","sample-asr-key",new InMemorySettingsStore(),secrets,verification,new(http,secrets,verification,[1]),recording,_ => { });
            var homeWindow = new HomeWindow(home,() => Task.FromResult(vm),() => Task.CompletedTask);
            var settingsWindow = new SettingsWindow(vm);
            try
            {
                homeWindow.Show(); homeWindow.UpdateLayout(); StaTest.Pump();
                Assert.True(homeWindow.IsVisible); Assert.False(home.AreServicesVerified);
                SavePreview(homeWindow,"home.png");
                settingsWindow.Show(); settingsWindow.UpdateLayout(); StaTest.Pump();
                Assert.Equal("sample-ai-key",vm.AiApiKey); Assert.Equal("sample-asr-key",vm.AsrApiKey);
                Assert.False(verification.CanStart(vm.BuildCandidate()));
                SavePreview(settingsWindow,"settings.png");
            }
            finally { settingsWindow.Close(); homeWindow.Close(); coordinator.DisposeAsync().AsTask().GetAwaiter().GetResult(); }
        });
    }
    private static void SavePreview(Window window,string name)
    {
        var directory = Environment.GetEnvironmentVariable("EXAMHELPER_TEST_UI_OUTPUT"); if(string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var bitmap = new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); bitmap.Render(window);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var stream = File.Create(Path.Combine(directory,name)); encoder.Save(stream);
    }
    private sealed class Host : IOverlayHost
    {
        public Task<ProtectionResult> OpenAsync(CancellationToken ct) => Task.FromResult(new ProtectionResult(false,5,0));
        public void Show() { } public void Close() { }
    }
}