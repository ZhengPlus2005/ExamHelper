using System.Collections.Immutable;
using System.Collections.ObjectModel;
using ExamHelper.App.Common;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Audio;
using ExamHelper.Windows.Settings;
namespace ExamHelper.App.Settings;
public sealed record ProviderOption(ProviderKind Kind,string Name);
public sealed class SettingsViewModel : ObservableObject,IAsyncDisposable
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
    private ServiceTestResult? _speechResult;
    private Guid? _speechOperation;
    private Task _speechProcessing = Task.CompletedTask;
    private bool _busy,_speechWorking,_disposed;
    private string _status = "",_result = "";
    public string AiBaseAddress { get; set; }
    public string AiModel { get; set; }
    public ProviderKind AiProviderKind { get; set; }
    public string AiApiKey { get; set; }
    public string AsrBaseAddress { get; set; }
    public string AsrModel { get; set; }
    public string AsrApiKey { get; set; }
    public string DeviceId { get; set; }
    public string DefaultCodeLanguage { get; set; }
    public string DirectCapturePrompt { get; set; }
    public double Opacity { get; set; }
    public ObservableCollection<HotkeyEditor> Hotkeys { get; } = [];
    public IReadOnlyList<ProviderOption> Providers { get; } = [new(ProviderKind.Kimi,"KIMI"),new(ProviderKind.Compatible,"自定义兼容接口")];
    public IReadOnlyList<MicrophoneDevice> Devices { get; set; } = [];
    public Func<float> ReadLevel { get; set; } = () => 0;
    public Func<IReadOnlyList<HotkeyBinding>,string?>? ProbeHotkeys { get; set; }
    public bool IsBusy { get => _busy; private set => Set(ref _busy,value); }
    public bool IsRecording => _recording.IsRecording;
    public bool IsSpeechWorking => _speechWorking;
    public double RecordingSeconds => _recording.Elapsed.TotalSeconds;
    public float MicrophoneLevel => ReadLevel();
    public bool CanConfirmSpeech => _speechResult?.RequiresConfirmation == true;
    public string Status { get => _status; set => Set(ref _status,value); }
    public string ResultText { get => _result; private set => Set(ref _result,value); }
    public string VerificationSummary
    {
        get { try { var c = BuildCandidate(); return $"AI 图片：{(_verification.IsAiVerified(c) ? "已验证" : "未验证")}　语音：{(_verification.IsAsrVerified(c) ? "已验证" : "未验证")}"; } catch { return "配置尚未完整"; } }
    }
    public SettingsViewModel(AppSettings initial,string aiKey,string asrKey,ISettingsStore store,ISecretStore secrets,ServiceVerification verification,
        ServiceTestRunner runner,RecordingController recording,Action<AppSettings> saved,Action<Action>? dispatch = null)
    {
        _initial = initial; _evidence = initial.Evidence; _store = store; _secrets = secrets; _verification = verification;
        _runner = runner; _recording = recording; _saved = saved; _dispatch = dispatch ?? (action => action());
        AiBaseAddress = initial.Ai.BaseUri.ToString(); AiModel = initial.Ai.Model; AiProviderKind = initial.Ai.ProviderKind; AiApiKey = aiKey;
        AsrBaseAddress = initial.Asr.BaseUri.ToString(); AsrModel = initial.Asr.Model; AsrApiKey = asrKey; DeviceId = initial.Asr.DeviceId;
        DefaultCodeLanguage = initial.DefaultCodeLanguage; DirectCapturePrompt = initial.DirectCapturePrompt; Opacity = initial.Ui.Opacity;
        foreach(var command in Enum.GetValues<ExamHelper.Core.Interaction.AppCommand>())
            Hotkeys.Add(new(command,HotkeyEditor.Format(initial.Hotkeys.Bindings.FirstOrDefault(b => b.Command == command))));
        _recording.Stopped += OnClip; _recording.Failed += OnRecordingFailed;
    }
    public AppSettings BuildCandidate() => _initial with
    {
        Ai = _initial.Ai with { BaseUri = new(AiBaseAddress.Trim()),Model = AiModel.Trim(),ProviderKind = AiProviderKind,SecretFingerprint = _verification.SecretDigest(AiApiKey) },
        Asr = _initial.Asr with { BaseUri = new(AsrBaseAddress.Trim()),Model = AsrModel.Trim(),DeviceId = DeviceId,SecretFingerprint = _verification.SecretDigest(AsrApiKey) },
        Ui = _initial.Ui with { Opacity = Opacity },DefaultCodeLanguage = DefaultCodeLanguage.Trim(),DirectCapturePrompt = DirectCapturePrompt,
        Hotkeys = new() { Bindings = Hotkeys.Select(row => row.Parse()).OfType<HotkeyBinding>().ToImmutableArray() },Evidence = _evidence
    };
    public async Task SaveAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        await _saveGate.WaitAsync(linked.Token);
        try
        {
            var aiKey = AiApiKey; var asrKey = AsrApiKey;
            var candidate = BuildCandidate(); JsonSettingsStore.Validate(candidate);
            if(!candidate.Hotkeys.Bindings.SequenceEqual(_initial.Hotkeys.Bindings) && ProbeHotkeys?.Invoke(candidate.Hotkeys.Bindings) is { Length: > 0 } conflict)
                throw new ArgumentException(conflict);
            await _secrets.SetAsync(candidate.Ai.SecretId,aiKey,linked.Token);
            await _secrets.SetAsync(candidate.Asr.SecretId,asrKey,linked.Token);
            await _store.SaveAsync(candidate,linked.Token); _initial = candidate; _saved(candidate);
            Status = "设置已保存。"; Notify();
        }
        finally { _saveGate.Release(); }
    }
    public async Task AutoSavePromptAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        await _saveGate.WaitAsync(linked.Token);
        try
        {
            var candidate = _initial with { DirectCapturePrompt = DirectCapturePrompt };
            await _store.SaveAsync(candidate,linked.Token); _initial = candidate; _saved(candidate);
        }
        finally { _saveGate.Release(); }
    }
    public Task RunTextTestAsync(CancellationToken ct) => RunAiTestAsync(false,ct);
    public Task RunImageTestAsync(CancellationToken ct) => RunAiTestAsync(true,ct);
    private async Task RunAiTestAsync(bool image,CancellationToken ct)
    {
        if(IsBusy || _speechWorking) return;
        IsBusy = true;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        try
        {
            await SaveAsync(linked.Token); var tested = _initial; Status = image ? "正在测试图片识别……" : "正在测试文字连接……";
            var result = image ? await _runner.TestImageAsync(tested,linked.Token) : await _runner.TestTextAsync(tested,linked.Token);
            if(_disposed) return;
            ResultText = result.Text;
            if(image && result.Passed)
            {
                try { var accepted = _verification.RecordAiSuccess(BuildCandidate(),result.Fingerprint); _evidence = accepted.Evidence; }
                catch(Exception error) when(error is InvalidOperationException or ArgumentException) { Status = "配置已变化，本次结果不计入验证资格，请重新测试。"; return; }
                await SaveAsync(linked.Token);
            }
            Status = result.Passed ? $"测试通过（{result.Elapsed.TotalSeconds:F1} 秒）。" : result.Error ?? "测试未通过。";
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
            await SaveAsync(ct); _speechSettings = _initial; _speechResult = null; _speechOperation = null;
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
            var result = await _runner.TestSpeechAsync(clip,_speechSettings!,_lifetime.Token);
            if(_disposed) return;
            _speechResult = result; ResultText = result.Text;
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
            _evidence = _verification.RecordAsrSuccess(BuildCandidate(),_speechResult.Fingerprint).Evidence;
            await SaveAsync(ct); _speechResult = _speechResult with { Passed = true,RequiresConfirmation = false };
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