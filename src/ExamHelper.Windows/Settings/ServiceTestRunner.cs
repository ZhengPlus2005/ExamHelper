using System.Diagnostics;
using System.Net.Http;
using System.Collections.Immutable;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Providers;
namespace ExamHelper.Windows.Settings;
public sealed record ServiceTestResult(bool Passed,string Text,string? Error,TimeSpan Elapsed,string Fingerprint,bool RequiresConfirmation = false,bool WasAttempted = true);
public sealed class ServiceTestRunner
{
    public IModelDiscovery ModelDiscovery { get; }
    private readonly AiGenerationGate _generationGate;
    private readonly HttpClient _http;
    private readonly MarkerStore _images;
    private readonly IAiClient _ai;
    private readonly IAsrClient _asr;
    private readonly ServiceVerification _verification;
    private readonly ImageRef _marker;
    public ServiceTestRunner(HttpClient http,ISecretStore secrets,ServiceVerification verification,byte[] markerPng,AiGenerationGate? generationGate = null)
    {
        _generationGate = generationGate ?? new(); ModelDiscovery = new ModelDiscoveryClient(http); _http = http; var images = _images = new MarkerStore(markerPng); _marker = images.Image;
        _ai = new ChatCompletionsClient(http,secrets,images); _asr = new AliyunAsrClient(http,secrets); _verification = verification;
    }
    public Task<ServiceTestResult> TestTextAsync(AppSettings settings,CancellationToken ct) => TestAiAsync(settings,false,ct);
    public Task<ServiceTestResult> TestImageAsync(AppSettings settings,CancellationToken ct) => TestAiAsync(settings,true,ct);
    public Task<ServiceTestResult> TestTextAsync(AppSettings settings,string key,CancellationToken ct) => TestAiAsync(settings,false,ct,new ChatCompletionsClient(_http,new FrozenSecret(settings.Ai.SecretId,key),_images));
    public Task<ServiceTestResult> TestImageAsync(AppSettings settings,string key,CancellationToken ct) => TestAiAsync(settings,true,ct,new ChatCompletionsClient(_http,new FrozenSecret(settings.Ai.SecretId,key),_images));
    private async Task<ServiceTestResult> TestAiAsync(AppSettings settings,bool image,CancellationToken ct,IAiClient? client = null)
    {
        var fingerprint = _verification.AiFingerprint(settings); var time = Stopwatch.StartNew();
        ct.ThrowIfCancellationRequested();
        using var lease = _generationGate.TryAcquire(Guid.NewGuid());
        if(lease is null) return new(false,"","已有回答或服务测试正在进行，请等待或停止。",time.Elapsed,fingerprint,WasAttempted:false);
        try
        {
            var request = new AiRequest(Guid.NewGuid(),[new(MessageRole.User,image ? "请只回复图片中的英文字母和数字，保持原顺序。" : "请用中文回复：连接测试成功。",image ? [_marker] : [])]);
            var text = new System.Text.StringBuilder(); bool complete = false;
            await foreach(var chunk in (client ?? _ai).StreamAsync(request,settings.Ai,ct).ConfigureAwait(false))
            {
                text.Append(chunk.Text); if(chunk.IsFinal) complete = chunk.IsComplete;
            }
            var output = text.ToString();
            var passed = complete && !string.IsNullOrWhiteSpace(output) && (!image || output.Contains("EH7Q9",StringComparison.Ordinal));
            return new(passed,output,passed ? null : image ? "未完整识别出测试图标记 EH7Q9。" : "服务未返回完整文字结果。",time.Elapsed,fingerprint);
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception error) { return new(false,"",SafeError(error),time.Elapsed,fingerprint); }
    }
    public Task<ServiceTestResult> TestSpeechAsync(RecordedAudio audio,AppSettings settings,string key,CancellationToken ct) => TestSpeechCoreAsync(audio,settings,ct,new AliyunAsrClient(_http,new FrozenSecret(settings.Asr.SecretId,key)));
    public Task<ServiceTestResult> TestSpeechAsync(RecordedAudio audio,AppSettings settings,CancellationToken ct) => TestSpeechCoreAsync(audio,settings,ct,_asr);
    private async Task<ServiceTestResult> TestSpeechCoreAsync(RecordedAudio audio,AppSettings settings,CancellationToken ct,IAsrClient client)
    {
        var fingerprint = _verification.AsrFingerprint(settings); var time = Stopwatch.StartNew();
        try
        {
            var text = await client.TranscribeAsync(audio,settings.Asr,ct).ConfigureAwait(false);
            var valid = !string.IsNullOrWhiteSpace(text);
            return new(false,text,valid ? null : "未识别到文字。",time.Elapsed,fingerprint,valid);
        }
        catch(OperationCanceledException) { throw; }
        catch(Exception error) { return new(false,"",SafeError(error),time.Elapsed,fingerprint); }
    }
    private static string SafeError(Exception error) => error is ServiceFailure ? error.Message : "服务测试失败，请检查配置或网络后重试。";
    private sealed class FrozenSecret(string id,string key) : ISecretStore
    {
        public Task<string?> GetAsync(string requested,CancellationToken ct) { ct.ThrowIfCancellationRequested(); return Task.FromResult<string?>(requested == id ? key : null); }
        public Task SetAsync(string id,string value,CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class MarkerStore(byte[] bytes) : IImageStore
    {
        private readonly byte[] _bytes = bytes.ToArray();
        public ImageRef Image { get; } = new(Guid.NewGuid(),"service-test.png",360,140);
        public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if(image.Id != Image.Id) throw new ArgumentException("测试图片不存在。",nameof(image));
            return Task.FromResult(_bytes.ToArray());
        }
        public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct) => Task.CompletedTask;
    }
}