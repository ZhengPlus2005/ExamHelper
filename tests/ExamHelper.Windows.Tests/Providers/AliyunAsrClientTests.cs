using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Providers;
using Xunit;
namespace ExamHelper.Windows.Tests.Providers;
public class AliyunAsrClientTests
{
    [Fact]
    public async Task SendsBase64ToBeijingWithoutLanguageAndParsesTranscript()
    {
        var file = Path.GetTempFileName(); await File.WriteAllBytesAsync(file, [1,2,3,4]);
        try
        {
            using var handler = new FakeHttpHandler(async (request,ct) =>
            {
                Assert.Equal("https://myworkspace.cn-beijing.maas.aliyuncs.com/compatible-mode/v1/chat/completions",request.RequestUri!.AbsoluteUri);
                Assert.Equal("Bearer",request.Headers.Authorization!.Scheme);
                using var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); var root = json.RootElement;
                Assert.Equal("qwen3-asr-flash",root.GetProperty("model").GetString());
                Assert.False(root.GetProperty("stream").GetBoolean());
                Assert.DoesNotContain("language",root.GetRawText());
                Assert.Equal("data:audio/wav;base64,AQIDBA==",root.GetProperty("messages")[0].GetProperty("content")[0].GetProperty("input_audio").GetProperty("data").GetString());
                return FakeHttpHandler.Json("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{\"content\":\"中文 and English\"}}]}");
            });
            using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("asr","test-key",default);
            var client = new AliyunAsrClient(http,secrets);
            var result = await client.TranscribeAsync(new(Guid.NewGuid(),file,TimeSpan.FromSeconds(1)),new AsrSettings { BaseUri = new("https://myworkspace.cn-beijing.maas.aliyuncs.com/compatible-mode/v1") },default);
            Assert.Equal("中文 and English",result); Assert.Equal(1,handler.Calls);
        }
        finally { File.Delete(file); }
    }
    [Theory]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"partial\"}}]}")]
    [InlineData("{\"choices\":[{\"finish_reason\":\"stop\",\"message\":{}}]}")]
    [InlineData("{\"choices\":\"wrong-type\"}")]
    public async Task RejectsMissingFinishOrMalformedRecognitionResponse(string body)
    {
        var file = Path.GetTempFileName(); await File.WriteAllBytesAsync(file,[1]);
        try
        {
            using var handler = new FakeHttpHandler((_,_) => Task.FromResult(FakeHttpHandler.Json(body)));
            using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("asr","test",default);
            var error = await Assert.ThrowsAsync<ServiceFailure>(() => new AliyunAsrClient(http,secrets).TranscribeAsync(new(Guid.NewGuid(),file,TimeSpan.FromSeconds(1)),new(),default));
            Assert.Equal(FailureCategory.InvalidPayload,error.Category);
        }
        finally { File.Delete(file); }
    }
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized,FailureCategory.Authentication)]
    [InlineData(HttpStatusCode.TooManyRequests,FailureCategory.RateLimit)]
    public async Task MapsFailuresWithoutAutomaticRetry(HttpStatusCode code,FailureCategory category)
    {
        var file = Path.GetTempFileName(); await File.WriteAllBytesAsync(file,[1]);
        try
        {
            using var handler = new FakeHttpHandler((_,_) => Task.FromResult(FakeHttpHandler.Json("{}",code)));
            using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("asr","test-key",default);
            var error = await Assert.ThrowsAsync<ServiceFailure>(() => new AliyunAsrClient(http,secrets).TranscribeAsync(new(Guid.NewGuid(),file,TimeSpan.FromSeconds(1)),new(),default));
            Assert.Equal(category,error.Category); Assert.Equal(1,handler.Calls);
        }
        finally { File.Delete(file); }
    }
    [Fact]
    public async Task TimeoutIsReportedButCallerCancellationStaysCancellation()
    {
        var file = Path.GetTempFileName(); await File.WriteAllBytesAsync(file,[1]);
        try
        {
            using var handler = new FakeHttpHandler(async (_,ct) => { await Task.Delay(Timeout.InfiniteTimeSpan,ct); return FakeHttpHandler.Json("{}"); });
            using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("asr","test",default);
            var client = new AliyunAsrClient(http,secrets,TimeSpan.FromMilliseconds(20));
            var clip = new RecordedAudio(Guid.NewGuid(),file,TimeSpan.FromSeconds(1));
            var error = await Assert.ThrowsAsync<ServiceFailure>(() => client.TranscribeAsync(clip,new(),default));
            Assert.Equal(FailureCategory.Timeout,error.Category);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.TranscribeAsync(clip,new(),cancel.Token));
        }
        finally { File.Delete(file); }
    }
}