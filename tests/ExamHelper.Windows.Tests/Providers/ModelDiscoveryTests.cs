using System.Net.Http;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Providers;
using Xunit;
namespace ExamHelper.Windows.Tests.Providers;
public class ModelDiscoveryTests
{
    [Fact] public async Task DiscoveryKeepsUnknownCapabilityUnknownAndNormalizesEndpoint()
    {
        using var http = new HttpClient(new FakeHttpHandler((r,_) =>
        {
            Assert.Equal("https://example.com/custom/v1/models",r.RequestUri!.AbsoluteUri); Assert.Equal("test-key",r.Headers.Authorization?.Parameter);
            return Task.FromResult(FakeHttpHandler.Json("{\"data\":[{\"id\":\"a\",\"supports_image_in\":true,\"supports_reasoning\":true},{\"id\":\"b\",\"effort\":{\"supported_levels\":[\"low\",\"high\",\"max\"]}},{\"id\":\"c\"}]}"));
        }));
        var models = await new ModelDiscoveryClient(http).ListAsync(new() { BaseUri = new("https://example.com/custom/v1/chat/completions") },"test-key",default);
        Assert.Equal(3,models.Count); Assert.True(models[0].SupportsImages); Assert.Null(models[0].EffortLevels);
        Assert.Equal(new[] { "low","high","max" },models[1].EffortLevels!.Value);
        Assert.Null(models[2].SupportsImages); Assert.Null(models[2].SupportsReasoning);
    }
}
