using System.Collections.Immutable;
using System.Collections.ObjectModel;
using ExamHelper.App.Common;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Audio;
using ExamHelper.Windows.Settings;
namespace ExamHelper.App.Settings;
public enum SettingsCloseChoice { Save, Discard, Cancel }
public sealed record ProviderOption(ProviderKind Kind,string Name);
public sealed partial class SettingsViewModel : ObservableObject,IAsyncDisposable
{
    private readonly ISettingsStore _store;
    private readonly SemaphoreSlim _saveGate = new(1);
    private readonly ISecretStore _secrets;
    private readonly ServiceVerification _verification;
    private readonly ServiceTestRunner _runner;
    private readonly RecordingController _recording;
    private readonly Action<AppSettings> _saved;
    private readonly Action<Action> _dispatch;
    private readonly CancellationTokenSource _lifetime = new();
    private AppSettings _initial;
    private ServiceEvidence _evidence;
    private AppSettings? _speechSettings;
    private string _speechKey = "";
    private ServiceTestResult? _speechResult;
    private Guid? _speechOperation;
    private Task _speechProcessing = Task.CompletedTask;
    private bool _busy,_speechWorking,_disposed;
    private string _status = "",_result = "",_speechResultText = "";
    private string _asrBaseAddress = "",_asrModel = "",_asrApiKey = "",_deviceId = "";
    private bool _captureProtectionEnabled;
    private AiProfileEditor? _selectedAiProfile;
    private Guid? _activeAiProfileId;
    public ObservableCollection<AiProfileEditor> AiProfiles { get; } = [];
    public AiProfileEditor? SelectedAiProfile
    {
        get => _selectedAiProfile;
        set { if(Set(ref _selectedAiProfile,value)) { NotifyAiFields(); Notify(nameof(HasSelectedAiProfile)); } }
    }
    public IReadOnlyList<ThinkingControl> ThinkingControls { get; } = Enum.GetValues<ThinkingControl>();
    public IReadOnlyList<ThinkingWireFormat> ThinkingFormats { get; } = Enum.GetValues<ThinkingWireFormat>();
    public bool HasSelectedAiProfile => SelectedAiProfile is not null;
    public string AiBaseAddress { get => SelectedAiProfile?.BaseAddress ?? ""; set { if(SelectedAiProfile is { } p) p.BaseAddress = value; } }
    public string AiModel { get => SelectedAiProfile?.Model ?? ""; set { if(SelectedAiProfile is { } p) p.Model = value; } }
    public ProviderKind AiProviderKind { get => SelectedAiProfile?.ProviderKind ?? ProviderKind.Kimi; set { if(SelectedAiProfile is { } p) p.ProviderKind = value; } }
    public string AiApiKey { get => SelectedAiProfile?.ApiKey ?? ""; set { if(SelectedAiProfile is { } p) p.ApiKey = value; } }
    public string AsrBaseAddress { get => _asrBaseAddress; set { if(Set(ref _asrBaseAddress,value)) NotifySpeechConfiguration(); } }
    public string AsrModel { get => _asrModel; set { if(Set(ref _asrModel,value)) NotifySpeechConfiguration(); } }
    public string AsrApiKey { get => _asrApiKey; set { if(Set(ref _asrApiKey,value)) NotifySpeechConfiguration(); } }
    public string DeviceId { get => _deviceId; set { if(Set(ref _deviceId,value)) NotifySpeechConfiguration(); } }
    public bool CaptureProtectionEnabled { get => _captureProtectionEnabled; set { if(Set(ref _captureProtectionEnabled,value)) Notify(nameof(HasUnsavedChanges)); } }
    public string DefaultCodeLanguage { get; set; }
    public string DirectCapturePrompt { get; set; }
    private double _opacity;
    public double Opacity { get => _opacity; set { if(Set(ref _opacity,value)) Notify(nameof(OpacityPercent)); } }
    public string OpacityPercent => Opacity.ToString("P0",System.Globalization.CultureInfo.CurrentCulture);
    public ObservableCollection<HotkeyEditor> Hotkeys { get; } = [];
    public IReadOnlyList<ProviderOption> Providers { get; } = [new(ProviderKind.Kimi,"KIMI"),new(ProviderKind.Compatible,"自定义兼容接口")];
    public IReadOnlyList<MicrophoneDevice> Devices { get; set; } = [];
    public Func<float> ReadLevel { get; set; } = () => 0;
    public Func<IReadOnlyList<HotkeyBinding>,string?>? ProbeHotkeys { get; set; }
    public bool IsBusy { get => _busy; private set => Set(ref _busy,value); }
    public bool IsRecording => _recording.IsRecording;
    public bool IsSpeechWorking => _speechWorking;
    public double RecordingSeconds => _recording.Elapsed.TotalSeconds;
    public float MicrophoneLevel => AudioPeakMeter.ToDisplayLevel(ReadLevel());
    public bool CanConfirmSpeech => _speechResult?.RequiresConfirmation == true;
    public string Status { get => _status; set => Set(ref _status,value); }
    public string ResultText { get => _result; private set => Set(ref _result,value); }
    public string SpeechResultText { get => _speechResultText; private set => Set(ref _speechResultText,value); }
    public string SpeechVerificationStatus
    {
        get
        {
            try { return _verification.IsAsrVerified(_initial with { Asr = BuildAsr(),Evidence = _evidence }) ? "语音：已验证" : "语音：未验证"; }
            catch(Exception error) when(error is ArgumentException or UriFormatException) { return "语音：未验证"; }
        }
    }
    private void NotifySpeechConfiguration() { Notify(nameof(SpeechVerificationStatus)); Notify(nameof(HasUnsavedChanges)); }
    public SettingsViewModel(AppSettings initial,string aiKey,string asrKey,ISettingsStore store,ISecretStore secrets,ServiceVerification verification,
        ServiceTestRunner runner,RecordingController recording,Action<AppSettings> saved,Action<Action>? dispatch = null,IReadOnlyDictionary<string,string>? aiKeys = null,ExamHelper.Windows.Providers.IModelDiscovery? discovery = null)
    {
        initial = AiProfileCatalog.Normalize(initial);
        _initial = initial; _evidence = initial.Evidence; _store = store; _secrets = secrets; _verification = verification;
        _runner = runner; _discovery = discovery ?? runner.ModelDiscovery; _recording = recording; _saved = saved; _dispatch = dispatch ?? (action => action());
        _activeAiProfileId = initial.ActiveAiProfileId;
        foreach(var profile in initial.AiProfiles!.Value)
            AddEditor(profile,aiKeys?.GetValueOrDefault(profile.Settings.SecretId) ?? (profile.Id == initial.ActiveAiProfileId ? aiKey : ""));
        SelectedAiProfile = AiProfiles.FirstOrDefault(p => p.Id == _activeAiProfileId) ?? AiProfiles.FirstOrDefault();
        AsrBaseAddress = initial.Asr.BaseUri.ToString(); AsrModel = initial.Asr.Model; AsrApiKey = asrKey; DeviceId = initial.Asr.DeviceId;
        DefaultCodeLanguage = initial.DefaultCodeLanguage; DirectCapturePrompt = initial.DirectCapturePrompt; Opacity = initial.Ui.Opacity; CaptureProtectionEnabled = initial.Ui.CaptureProtectionEnabled;
        foreach(var command in Enum.GetValues<ExamHelper.Core.Interaction.AppCommand>().Where(c => c != ExamHelper.Core.Interaction.AppCommand.HideOverlay))
            Hotkeys.Add(new(command,HotkeyEditor.Format(initial.Hotkeys.Bindings.FirstOrDefault(b => b.Command == command))));
        _initial = BuildCandidate();
        _recording.Stopped += OnClip; _recording.Failed += OnRecordingFailed;
    }
    public AiProfileEditor AddAiProfile()
    {
        var profile = new AiServiceProfile { Name = "",Settings = new() { ProviderKind = ProviderKind.Compatible,SecretId = "ai-" + Guid.NewGuid().ToString("N") } };
        var editor = AddEditor(profile,""); SelectedAiProfile = editor; Notify(nameof(HasUnsavedChanges)); return editor;
    }
    public void DeleteSelectedAiProfile()
    {
        if(SelectedAiProfile is not { } profile) return;
        if(_activeAiProfileId == profile.Id) _activeAiProfileId = null;
        AiProfiles.Remove(profile); SelectedAiProfile = AiProfiles.FirstOrDefault(); UpdateActiveMarkers(); Notify();
    }
    public void ActivateSelectedAiProfile()
    {
        if(SelectedAiProfile is not { } profile) return;
        _activeAiProfileId = profile.Id; UpdateActiveMarkers();
        Status = "已设为考试默认配置，保存并返回后生效。"; Notify();
    }
    private AiProfileEditor AddEditor(AiServiceProfile profile,string key)
    {
        var editor = new AiProfileEditor(profile,key,_verification) { IsActive = profile.Id == _activeAiProfileId };
        editor.PropertyChanged += (_,e) =>
        {
            if(ReferenceEquals(editor,SelectedAiProfile) && e.PropertyName is nameof(AiProfileEditor.BaseAddress) or nameof(AiProfileEditor.Model) or nameof(AiProfileEditor.ProviderKind) or nameof(AiProfileEditor.ApiKey)) NotifyAiFields();
            Notify(nameof(HasUnsavedChanges));
        };
        AiProfiles.Add(editor); return editor;
    }
    private void UpdateActiveMarkers() { foreach(var p in AiProfiles) p.IsActive = p.Id == _activeAiProfileId; }
    private void NotifyAiFields()
    {
        Notify(nameof(AiBaseAddress)); Notify(nameof(AiModel)); Notify(nameof(AiProviderKind)); Notify(nameof(AiApiKey));
    }
    public AppSettings BuildCandidate() => AiProfileCatalog.Normalize(_initial with
    {
        AiProfiles = AiProfiles.Select(p => p.BuildProfile()).ToImmutableArray(),ActiveAiProfileId = _activeAiProfileId,
        Asr = _initial.Asr with { BaseUri = new(AsrBaseAddress.Trim()),Model = AsrModel.Trim(),DeviceId = DeviceId,SecretFingerprint = _verification.SecretDigest(AsrApiKey) },
        Ui = _initial.Ui with { Opacity = Opacity,CaptureProtectionEnabled = CaptureProtectionEnabled },DefaultCodeLanguage = DefaultCodeLanguage.Trim(),DirectCapturePrompt = DirectCapturePrompt,
        Hotkeys = new() { Bindings = Hotkeys.Select(row => row.Parse()).OfType<HotkeyBinding>().ToImmutableArray() },Evidence = _evidence
    });
    public bool HasUnsavedChanges
    {
        get
        {
            try { return System.Text.Json.JsonSerializer.Serialize(BuildCandidate()) != System.Text.Json.JsonSerializer.Serialize(_initial); }
            catch { return true; }
        }
    }
    public Task RunTextTestAsync(CancellationToken ct) => RunAiTestAsync(false,ct);
    public Task RunImageTestAsync(CancellationToken ct) => RunAiTestAsync(true,ct);
    private async Task RunAiTestAsync(bool image,CancellationToken ct)
    {
        if(IsBusy || _speechWorking || SelectedAiProfile is not { } profile) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        try
        {
            var testedModelId = profile.SelectedModel?.Id;
            var tested = profile.TestSettings(); var key = profile.ApiKey; var snapshot = profile.BuildProfile();
            Status = $"正在测试 {profile.Name} 的" + (image ? "图片识别……" : "文字连接……");
            var result = image ? await _runner.TestImageAsync(tested,key,linked.Token) : await _runner.TestTextAsync(tested,key,linked.Token);
            if(_disposed) return;
            if(!result.WasAttempted) { Status = result.Error ?? "测试未开始。"; return; }
            ResultText = result.Text;
            if(!AiProfiles.Contains(profile) || !profile.Models.Any(m => m.Id == testedModelId)) { Status = "配置已删除，本次测试结果已丢弃。"; return; }
            if(!await PersistAiTestAsync(profile,snapshot,testedModelId!.Value,key,image ? AiTestKind.Image : AiTestKind.Text,result.Passed,result.Fingerprint,linked.Token)) return;
            Status = result.Passed ? $"{profile.Name} 测试通过（{result.Elapsed.TotalSeconds:F1} 秒）。" : result.Error ?? "测试未通过。";
        }
        catch(OperationCanceledException) { if(!_disposed) Status = "测试已取消。"; }
        catch(Exception error) { Status = Explain(error); }
        finally { IsBusy = false; Notify(); }
    }
    public async Task ToggleSpeechTestAsync(CancellationToken ct)
    {
        if(_recording.IsRecording)
        {
            await _recording.StopAsync(ct); await Task.Yield(); await _speechProcessing; return;
        }
        if(_speechWorking || IsBusy) return;
        try
        {
            _speechSettings = _initial with { Asr = BuildAsr() }; _speechKey = AsrApiKey; _speechResult = null; _speechOperation = null; SpeechResultText = "";
            _speechWorking = true; Status = "正在录音，请说一句中英混合的话；再次点击停止，最多 60 秒。"; Notify();
            await _recording.StartAsync(DeviceId,_lifetime.Token);
        }
        catch(Exception error) { _speechWorking = false; Status = Explain(error); Notify(); }
    }
    private void OnClip(RecordedAudio clip) => _dispatch(() =>
    {
        if(!_disposed && _speechOperation != clip.OperationId) { _speechOperation = clip.OperationId; _speechProcessing = FinishSpeechAsync(clip); }
    });
    private async Task FinishSpeechAsync(RecordedAudio clip)
    {
        try
        {
            Status = "正在识别测试录音……"; Notify();
            var result = await _runner.TestSpeechAsync(clip,_speechSettings!,_speechKey,_lifetime.Token);
            if(_disposed) return;
            _speechResult = result; SpeechResultText = result.Text;
            Status = result.RequiresConfirmation ? "请核对识别文字，正确后点击“识别正确”。" : result.Error ?? "识别测试未通过。";
        }
        catch(OperationCanceledException) { }
        catch(Exception error) { if(!_disposed) Status = Explain(error); }
        finally { await _recording.CancelAsync(); _speechWorking = false; Notify(); }
    }
    public async Task ConfirmSpeechAsync(CancellationToken ct)
    {
        if(_speechResult?.RequiresConfirmation != true || string.IsNullOrWhiteSpace(_speechResult.Text)) return;
        try
        {
            _verification.RecordAsrSuccess(_initial with { Asr = BuildAsr() },_speechResult.Fingerprint);
            if(!await PersistSpeechTestAsync(ct)) return; _speechResult = _speechResult with { Passed = true,RequiresConfirmation = false };
            Status = "语音识别测试已确认并保存。"; Notify();
        }
        catch(Exception error) { Status = Explain(error); }
    }
    private void OnRecordingFailed(Exception error) => _dispatch(() => { if(!_disposed) { Status = Explain(error); _speechWorking = false; Notify(); } });
    public void RefreshTelemetry() { Notify(nameof(IsRecording)); Notify(nameof(RecordingSeconds)); Notify(nameof(MicrophoneLevel)); }
    public async ValueTask DisposeAsync()
    {
        if(_disposed) return; _disposed = true; _lifetime.Cancel();
        _recording.Stopped -= OnClip; _recording.Failed -= OnRecordingFailed;
        await _recording.DisposeAsync(); _lifetime.Dispose();
    }
}

