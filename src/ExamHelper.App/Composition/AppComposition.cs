using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Windows;
using ExamHelper.App.Home;
using ExamHelper.App.Overlay;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Drafts;
using ExamHelper.Windows.Images;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Audio;
using ExamHelper.Windows.Capture;
using ExamHelper.Windows.Native;
using ExamHelper.Windows.Persistence;
using ExamHelper.Windows.Providers;
using ExamHelper.Windows.Settings;
namespace ExamHelper.App.Composition;
public sealed class AppComposition : IAsyncDisposable
{
    private volatile AppSettings _settings = new();
    private readonly HttpClient _http = new(new SocketsHttpHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
    public AppSettings CurrentSettings => _settings;
    public JsonSettingsStore SettingsStore { get; } = new(AppDataPaths.Settings);
    public UserSecretStore Secrets { get; } = new(AppDataPaths.Secrets);
    public SqliteSessionStore Sessions { get; }
    public FileImageStore Images { get; }
    public ImageImportService ImageImporter { get; }
    public ImageReferenceRegistry ImageReferences { get; } = new();
    public AiGenerationGate GenerationGate { get; } = new();
    private AiGenerationGate CaptureAudioGate { get; } = new();
    public ConversationWorkspace Workspace { get; private set; } = null!;
    public ServiceVerification Verification { get; private set; } = null!;
    public SessionCoordinator Coordinator => Workspace.Selected ?? throw new InvalidOperationException("尚未选择对话。");
    public IOverlayHost Overlay { get; private set; } = null!;
    public HomeViewModel Home { get; private set; } = null!;
    private readonly List<MicrophoneRecorder> _microphones = [];
    private AppComposition()
    {
        Sessions=new(AppDataPaths.Database,ImageReferences); Images=new(AppDataPaths.Images,ImageReferences); ImageImporter=new(Images);
    }
    private SessionCoordinator CreateCoordinator()
    {
        var microphone=new MicrophoneRecorder(AppDataPaths.TempAudio); _microphones.Add(microphone);
        return new(new ChatCompletionsClient(_http,Secrets,Images),new AliyunAsrClient(_http,Secrets),new ExclusiveMicrophoneRecorder(microphone,CaptureAudioGate),
            new FullScreenCapture(windowHandle: () => (Overlay as OverlayHost)?.CurrentWindow?.NativeHandle ?? 0),Images,() => CurrentSettings,Sessions,TimeProvider.System,GenerationGate,Verification);
    }
    private ImageReferenceCleaner _cleaner = null!;
    public static async Task<AppComposition> CreateAsync(Func<AppComposition,IOverlayHost>? overlayFactory = null)
    {
        var result = new AppComposition(); var warnings = new List<string>();
        try
        {
            var key = await result.Secrets.GetAsync("verification-hmac",CancellationToken.None);
            if(key is null) { key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)); await result.Secrets.SetAsync("verification-hmac",key,CancellationToken.None); }
            result.Verification = new(Convert.FromBase64String(key));
            try { result._settings = await result.SettingsStore.LoadAsync(CancellationToken.None); } catch(ServiceFailure error) { warnings.Add(error.Message); }
            try { result._settings = await result.ReadSettingsForEntryAsync(); } catch(ServiceFailure error) { warnings.Add(error.Message); }
            result._cleaner = new(result.Sessions,result.Images,result.ImageReferences,() => result.Workspace?.LiveImageIds() ?? new HashSet<Guid>());
            try
            {
                await result.Sessions.RecoverInterruptedAsync(CancellationToken.None);
                AppDataPaths.CleanTemporaryAudio(AppDataPaths.TempAudio);
                await result._cleaner.ReconcileAsync(CancellationToken.None);
            }
            catch(Exception error) { warnings.Add(ObservableMessage(error)); }
            result.Workspace=new(result.Sessions,result.CreateCoordinator);
            await result.Workspace.NewAsync(false,CancellationToken.None);
            result.Overlay = overlayFactory?.Invoke(result) ?? new OverlayHost(result.Coordinator,result.Images,() => result.CurrentSettings,result.SaveUiAsync,
                () => result.Home.ReturnedAsync(CancellationToken.None),async () => { await result.DisposeAsync(); Application.Current.Shutdown(); },
                report: message => result.Home.Status = message,workspace: result.Workspace,verification: result.Verification);
            result.Home = new(result.Workspace,result.Sessions,result.Images,() => Task.FromResult(result.CurrentSettings),result.Verification,result.Overlay,
                () => result._cleaner.ReconcileAsync(CancellationToken.None),result.ReadSettingsForEntryAsync);
            result.Home.Status = string.Join(Environment.NewLine,warnings);
            return result;
        }
        catch { result._http.Dispose(); throw; }
    }
    public async Task<AppSettings> ReadSettingsForEntryAsync()
    {
        _settings = await Verification.RefreshSecretDigestsAsync(_settings,Secrets,CancellationToken.None); return _settings;
    }
    public async Task SaveUiAsync(UiSettings ui)
    {
        var candidate = _settings with { Ui = ui }; await SettingsStore.SaveAsync(candidate,CancellationToken.None); _settings = candidate;
    }
    public async Task<SettingsViewModel> CreateSettingsViewModelAsync()
    {
        _settings = AiProfileCatalog.Normalize(_settings);
        string ai = "",asr = "",warning = "";
        var profileKeys = new Dictionary<string,string>();
        foreach(var profile in _settings.AiProfiles!.Value)
        {
            try { profileKeys[profile.Settings.SecretId] = await Secrets.GetAsync(profile.Settings.SecretId,CancellationToken.None) ?? ""; }
            catch(ServiceFailure error) { warning += error.Message; }
        }
        try { ai = await Secrets.GetAsync(_settings.Ai.SecretId,CancellationToken.None) ?? ""; } catch(ServiceFailure error) { warning += error.Message; }
        try { asr = await Secrets.GetAsync(_settings.Asr.SecretId,CancellationToken.None) ?? ""; } catch(ServiceFailure error) { warning += error.Message; }
        var microphone = new MicrophoneRecorder(AppDataPaths.TempAudio);
        var recording = new RecordingController(new ExclusiveMicrophoneRecorder(microphone,CaptureAudioGate),TimeProvider.System);
        var resource = Application.GetResourceStream(new Uri("pack://application:,,,/ExamHelper.App;component/Assets/service-test.png"));
        using var marker = new MemoryStream(); using(resource.Stream) await resource.Stream.CopyToAsync(marker);
        var runner = new ServiceTestRunner(_http,Secrets,Verification,marker.ToArray(),GenerationGate);
        var dispatcher = Application.Current.Dispatcher;
        var viewModel = new SettingsViewModel(_settings,ai,asr,SettingsStore,Secrets,Verification,runner,recording,
            saved => _settings = saved,action => dispatcher.BeginInvoke(action),profileKeys) { ReadLevel = () => microphone.PeakLevel,ProbeHotkeys = HotkeyConflictProbe.Check };
        var devices = new List<MicrophoneDevice> { new("","系统默认麦克风") };
        try { devices.AddRange(MicrophoneRecorder.GetDevices()); } catch { warning += "无法列出麦克风，请检查 Windows 权限。"; }
        if(!string.IsNullOrEmpty(_settings.Asr.DeviceId) && !devices.Any(d => d.Id == _settings.Asr.DeviceId))
            devices.Add(new(_settings.Asr.DeviceId,"已保存的麦克风（当前不可用）"));
        viewModel.Devices = devices; viewModel.Status = warning;
        return viewModel;
    }
    public async ValueTask DisposeAsync()
    {
        Overlay?.Close();
        if(Workspace is not null) await Workspace.DisposeAsync();
        foreach(var microphone in _microphones) await microphone.DisposeAsync();
        _http.Dispose();
    }
    private static string ObservableMessage(Exception error) => error is ServiceFailure ? error.Message : "部分本地恢复未完成，请检查存储后重试。";
}
