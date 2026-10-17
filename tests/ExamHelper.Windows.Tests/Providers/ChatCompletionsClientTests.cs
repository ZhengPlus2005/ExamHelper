using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Common;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Providers;
using Xunit;
namespace ExamHelper.Windows.Tests.Providers;
public class ChatCompletionsClientTests
{
    private static AiRequest Request() => new(Guid.NewGuid(),[new(MessageRole.User,"题目",[])]);
    [Fact]
    public async Task ParsesSplitUtf8AndSseEventsAndIgnoresReasoning()
    {
        using var handler = new FakeHttpHandler((_,_) => Task.FromResult(Sse(
            ": keepalive\n\ndata: {\"choices\":[{\"delta\":{\"reasoning_content\":\"隐藏推理\",\"content\":\"答案😀\"}}]}\n\n" +
            "data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}\n\n" +
            "data: {\"choices\":[],\"usage\":{\"total_tokens\":1}}\n\ndata: [DONE]\n\n")));
        using var http = new HttpClient(handler); var secrets = await Secrets();
        var chunks = await Collect(new ChatCompletionsClient(http,secrets,new Images()).StreamAsync(Request(),new(),default));
        Assert.Equal("答案😀",string.Concat(chunks.Select(c => c.Text)));
        Assert.True(chunks[^1].IsFinal); Assert.True(chunks[^1].IsComplete); Assert.Equal(1,handler.Calls);
    }
    [Theory]
    [InlineData("data: {\"choices\":[{\"delta\":{\"content\":\"片段\"}}]}\n\n")]
    [InlineData("data: [DONE]\n\n")]
    public async Task EofWithoutFinishIsIncomplete(string body)
    {
        using var handler = new FakeHttpHandler((_,_) => Task.FromResult(Sse(body)));
        using var http = new HttpClient(handler);
        var client = new ChatCompletionsClient(http,await Secrets(),new Images());
        var error = await Assert.ThrowsAsync<ServiceFailure>(() => Collect(client.StreamAsync(Request(),new(),default)));
        Assert.Equal(FailureCategory.Transport,error.Category);
    }
    [Theory]
    [InlineData(ProviderKind.Kimi,true)]
    [InlineData(ProviderKind.Compatible,false)]
    public async Task PreservesImageOrderAndOnlyKimiPresetSendsThinkingOption(ProviderKind provider,bool thinking)
    {
        var images = new Images(); var a = new ImageRef(Guid.NewGuid(),"a",1,1); var b = new ImageRef(Guid.NewGuid(),"b",1,1);
        images.Bytes[a.Id] = [1]; images.Bytes[b.Id] = [2];
        using var handler = new FakeHttpHandler(async (request,ct) =>
        {
            Assert.Equal("https://example.com/custom/v1/chat/completions",request.RequestUri!.AbsoluteUri);
            using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var root = json.RootElement; Assert.Equal(thinking,root.TryGetProperty("thinking",out _));
            Assert.False(root.TryGetProperty("temperature",out _));
            var parts = root.GetProperty("messages")[0].GetProperty("content");
            Assert.EndsWith("AQ==",parts[1].GetProperty("image_url").GetProperty("url").GetString());
            Assert.EndsWith("Ag==",parts[2].GetProperty("image_url").GetProperty("url").GetString());
            return Sse("data: {\"choices\":[{\"delta\":{},\"finish_reason\":\"length\"}]}\n\ndata: [DONE]\n\n");
        });
        using var http = new HttpClient(handler);
        var client = new ChatCompletionsClient(http,await Secrets(),images);
        var chunks = await Collect(client.StreamAsync(new(Guid.NewGuid(),[new(MessageRole.User,"图片",[a,b])]),new AiSettings { BaseUri = new("https://example.com/custom/v1/chat/completions"), ProviderKind = provider },default));
        Assert.True(chunks[^1].IsFinal); Assert.False(chunks[^1].IsComplete);
    }
    [Fact]
    public async Task ProviderErrorsNeverAutoRetry()
    {
        using var handler = new FakeHttpHandler((_,_) => Task.FromResult(FakeHttpHandler.Json("{}",HttpStatusCode.TooManyRequests)));
        using var http = new HttpClient(handler); var client = new ChatCompletionsClient(http,await Secrets(),new Images());
        var error = await Assert.ThrowsAsync<ServiceFailure>(() => Collect(client.StreamAsync(Request(),new(),default)));
        Assert.Equal(FailureCategory.RateLimit,error.Category); Assert.Equal(1,handler.Calls);
    }
    [Fact]
    public async Task CancelsWhileWaitingAndIdleTimeoutIsFailure()
    {
        using var handler = new FakeHttpHandler(async (_,ct) => { await Task.Delay(Timeout.InfiniteTimeSpan,ct); return Sse(""); });
        using var http = new HttpClient(handler); var client = new ChatCompletionsClient(http,await Secrets(),new Images(),TimeSpan.FromMilliseconds(30));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Collect(client.StreamAsync(Request(),new(),cancel.Token)));
        var error = await Assert.ThrowsAsync<ServiceFailure>(() => Collect(client.StreamAsync(Request(),new(),default)));
        Assert.Equal(FailureCategory.Timeout,error.Category);
    }
    private static HttpResponseMessage Sse(string body) => new(HttpStatusCode.OK) { Content = new StreamContent(new ChunkedReadStream(Encoding.UTF8.GetBytes(body),1)) };
    private static async Task<InMemorySecretStore> Secrets() { var store = new InMemorySecretStore(); await store.SetAsync("ai","test-key",default); return store; }
    private static async Task<List<AiChunk>> Collect(IAsyncEnumerable<AiChunk> stream) { var result = new List<AiChunk>(); await foreach(var part in stream) result.Add(part); return result; }
    private sealed class Images : IImageStore
    {
        public Dictionary<Guid,byte[]> Bytes { get; } = [];
        public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct) => Task.FromResult(Bytes[image.Id]);
        public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct) => Task.CompletedTask;
    }
}