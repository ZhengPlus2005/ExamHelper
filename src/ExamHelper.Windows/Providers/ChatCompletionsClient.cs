using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Providers;
public sealed class ChatCompletionsClient(HttpClient http,ISecretStore secrets,IImageStore images,TimeSpan? idleTimeout = null,TimeSpan? totalTimeout = null) : IAiClient
{
    public async Task<IAiClient> PrepareAsync(AiSettings settings,CancellationToken ct)
    {
        var key = await secrets.GetAsync(settings.SecretId,ct).ConfigureAwait(false);
        if(string.IsNullOrWhiteSpace(key)) throw new ServiceFailure(FailureCategory.Authentication,"请先配置 AI 服务密钥。");
        return new ChatCompletionsClient(http,new FrozenSecretStore(settings.SecretId,key),images,idleTimeout,totalTimeout);
    }
    public async IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request,AiSettings settings,[EnumeratorCancellation] CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var endpoint = ProviderEndpoint.ChatCompletions(settings.BaseUri);
        var key = await secrets.GetAsync(settings.SecretId,ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(key)) throw new ServiceFailure(FailureCategory.Authentication,"请先配置 AI 服务密钥。");
        using var total = CancellationTokenSource.CreateLinkedTokenSource(ct); total.CancelAfter(totalTimeout ?? TimeSpan.FromMinutes(10));
        using var read = CancellationTokenSource.CreateLinkedTokenSource(total.Token);
        var messages = new List<object>();
        long imageBytes = 0;
        foreach (var message in request.Messages)
        {
            object content = message.Text;
            if (message.Images.Length > 0)
            {
                var parts = new List<object>();
                if (message.Text.Length > 0) parts.Add(new { type = "text",text = message.Text });
                foreach (var image in message.Images)
                {
                    byte[] bytes;
                    try { bytes = await images.ReadAsync(image,total.Token).ConfigureAwait(false); }
                    catch (IOException) { throw new ServiceFailure(FailureCategory.Storage,"无法读取历史截图，请检查本地图片文件。"); }
                    imageBytes += (bytes.LongLength + 2) / 3 * 4;
                    if (imageBytes > 100_000_000) throw new ServiceFailure(FailureCategory.InvalidPayload,"图片请求超过 100MB，请减少截图。");
                    parts.Add(new { type = "image_url",image_url = new { url = "data:image/png;base64," + Convert.ToBase64String(bytes) } });
                }
                content = parts;
            }
            var item = new Dictionary<string,object> { ["role"] = message.Role.ToString().ToLowerInvariant(),["content"] = content };
            if(message.Role == ExamHelper.Core.Sessions.MessageRole.Assistant && message.ReasoningContent is not null) item["reasoning_content"] = message.ReasoningContent;
            messages.Add(item);
        }
        var payload = new Dictionary<string,object> { ["model"] = settings.Model,["messages"] = messages,["stream"] = true };
        ThinkingRequestWriter.Apply(payload,settings);
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        if (json.LongLength > 100_000_000) throw new ServiceFailure(FailureCategory.InvalidPayload,"请求超过客户端 100MB 上限，请减少截图或新建对话。");
        using var messageRequest = new HttpRequestMessage(HttpMethod.Post,endpoint);
        messageRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer",key);
        messageRequest.Content = new ByteArrayContent(json); messageRequest.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
        read.CancelAfter(idleTimeout ?? TimeSpan.FromSeconds(60));
        using var response = await SendAsync(messageRequest,read.Token,ct).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode) throw await ProviderErrorMapper.FromResponseAsync(response,key,read.Token).ConfigureAwait(false);
        var stream = await response.Content.ReadAsStreamAsync(read.Token).ConfigureAwait(false);
        await using var events = SseReader.ReadEventsAsync(stream,read.Token).GetAsyncEnumerator(read.Token);
        string? finish = null;
        while (await NextAsync(events,read,ct).ConfigureAwait(false))
        {
            var data = events.Current;
            if (data == "[DONE]") break;
            var (text,reason,reasoning) = Parse(data);
            if (text.Length > 0 || reasoning is not null) yield return new(text,false,ReasoningContent: reasoning);
            if (reason is not null) finish = reason;
        }
        if (finish is null) throw new ServiceFailure(FailureCategory.Transport,"回答流中断，未收到完成标记，请手动重试。");
        yield return new("",true,finish == "stop");
    }
    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token,CancellationToken caller)
    {
        try { return await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw new ServiceFailure(FailureCategory.Timeout,"AI 请求超时，请手动重试。"); }
        catch (HttpRequestException) { throw new ServiceFailure(FailureCategory.Transport,"无法连接 AI 服务，请检查网络。"); }
    }
    private async Task<bool> NextAsync(IAsyncEnumerator<string> events,CancellationTokenSource read,CancellationToken caller)
    {
        read.CancelAfter(idleTimeout ?? TimeSpan.FromSeconds(60));
        try { return await events.MoveNextAsync().ConfigureAwait(false); }
        catch (OperationCanceledException) when (!caller.IsCancellationRequested) { throw new ServiceFailure(FailureCategory.Timeout,"AI 回答等待超时，请手动重试。"); }
        catch (IOException) { throw new ServiceFailure(FailureCategory.Transport,"AI 回答连接中断，请手动重试。"); }
        finally { read.CancelAfter(Timeout.InfiniteTimeSpan); }
    }
    private static (string Text,string? Finish,string? Reasoning) Parse(string data)
    {
        try
        {
            using var document = JsonDocument.Parse(data); var root = document.RootElement;
            if (root.TryGetProperty("error",out _)) throw new ServiceFailure(FailureCategory.Transport,"AI 服务在回答中报告错误，请手动重试。");
            var choices = root.GetProperty("choices");
            if (choices.GetArrayLength() == 0) return ("",null,null);
            var choice = choices[0]; string text = "",finish = ""; string? reasoning = null;
            if (choice.TryGetProperty("delta",out var delta) && delta.TryGetProperty("content",out var content) && content.ValueKind == JsonValueKind.String)
                text = content.GetString() ?? "";
            if(choice.TryGetProperty("delta",out var deltaReason) && deltaReason.TryGetProperty("reasoning_content",out var reasoningValue) && reasoningValue.ValueKind == JsonValueKind.String)
                reasoning = reasoningValue.GetString();
            if (choice.TryGetProperty("finish_reason",out var reason) && reason.ValueKind == JsonValueKind.String) finish = reason.GetString() ?? "";
            return (text,finish.Length > 0 ? finish : null,reasoning);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new ServiceFailure(FailureCategory.InvalidPayload,"AI 服务返回了无法解析的流数据。"); }
    }
}