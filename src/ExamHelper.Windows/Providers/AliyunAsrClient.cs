using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Providers;
public sealed class AliyunAsrClient(HttpClient http, ISecretStore secrets, TimeSpan? timeout = null) : IAsrClient
{
    public async Task<string> TranscribeAsync(RecordedAudio audio, AsrSettings settings, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (!ProviderEndpoint.IsBeijingAsr(settings.BaseUri))
            throw new ServiceFailure(FailureCategory.InvalidPayload, "语音服务仅接受阿里云北京地域地址。");
        var endpoint = ProviderEndpoint.ChatCompletions(settings.BaseUri);
        var secret = await secrets.GetAsync(settings.SecretId, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(secret)) throw new ServiceFailure(FailureCategory.Authentication, "请先配置语音服务密钥。");
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(timeout ?? TimeSpan.FromSeconds(90));
        try
        {
            var length = new FileInfo(audio.TemporaryPath).Length;
            if (length == 0 || length > 7_000_000)
                throw new ServiceFailure(FailureCategory.InvalidPayload, "录音为空或超出服务音频大小限制。");
            var bytes = await File.ReadAllBytesAsync(audio.TemporaryPath, limit.Token).ConfigureAwait(false);
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            request.Content = JsonContent.Create(new
            {
                model = settings.Model,
                messages = new[] { new { role = "user", content = new[] { new { type = "input_audio", input_audio = new { data = "data:audio/wav;base64," + Convert.ToBase64String(bytes) } } } } },
                stream = false,
                asr_options = new { enable_itn = false }
            });
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, limit.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw ProviderErrorMapper.FromStatus(response.StatusCode);
            await using var body = await response.Content.ReadAsStreamAsync(limit.Token).ConfigureAwait(false);
            using var json = await JsonDocument.ParseAsync(body, cancellationToken: limit.Token).ConfigureAwait(false);
            if (!json.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                throw new ServiceFailure(FailureCategory.InvalidPayload, "语音服务未返回识别结果。");
            var choice = choices[0];
            if (!choice.TryGetProperty("finish_reason", out var finish) || finish.GetString() != "stop")
                throw new ServiceFailure(FailureCategory.InvalidPayload, "语音服务未完整返回识别结果。");
            var content = choice.GetProperty("message").GetProperty("content");
            var transcript = content.ValueKind == JsonValueKind.String ? content.GetString() :
                content.ValueKind == JsonValueKind.Array ? string.Concat(content.EnumerateArray().Where(p => p.TryGetProperty("text", out _)).Select(p => p.GetProperty("text").GetString())) : null;
            if (string.IsNullOrWhiteSpace(transcript)) throw new ServiceFailure(FailureCategory.InvalidPayload, "未识别到文字，请重试或丢弃录音。");
            return transcript;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ServiceFailure(FailureCategory.Timeout, "语音识别超时，请手动重试。"); }
        catch (HttpRequestException) { throw new ServiceFailure(FailureCategory.Transport, "无法连接语音服务，请检查网络后手动重试。"); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException) { throw new ServiceFailure(FailureCategory.InvalidPayload, "语音服务返回了无法解析的数据。"); }
    }
}