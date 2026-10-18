using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Providers;
using Xunit;
namespace ExamHelper.Windows.Tests.Providers;
public class ThinkingRequestTests
{
    [Theory]
    [InlineData("kimi-k3",ThinkingKind.Effort,"high","reasoning_effort","high")]
    [InlineData("kimi-k2.6",ThinkingKind.Disabled,null,"thinking","disabled")]
    [InlineData("kimi-k2.6",ThinkingKind.Enabled,null,"thinking","enabled")]
    [InlineData("kimi-k3",ThinkingKind.ServiceDefault,null,null,null)]
    [InlineData("unknown",ThinkingKind.ServiceDefault,null,null,null)]
    public async Task ExplicitThinkingUsesOnlyItsSupportedProtocol(string model,ThinkingKind kind,string? effort,string? field,string? expected)
    {
        var ai = new AiSettings { Model = model,Thinking = new(kind,effort),ThinkingCapability = ThinkingCapability.For(new() { Name = model }) };
        using var http = new HttpClient(new FakeHttpHandler(async (r,ct) =>
        {
            using var json = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct)); var root = json.RootElement;
            Assert.Equal(field == "thinking",root.TryGetProperty("thinking",out var thinking));
            Assert.Equal(field == "reasoning_effort",root.TryGetProperty("reasoning_effort",out var level));
            if(field == "thinking") Assert.Equal(expected,thinking.GetProperty("type").GetString());
            if(field == "reasoning_effort") Assert.Equal(expected,level.GetString());
            return Reply();
        }));
        var secrets = new InMemorySecretStore(); await secrets.SetAsync("ai","test-key",default);
        await foreach(var _ in new ChatCompletionsClient(http,secrets,new StubServices()).StreamAsync(new(Guid.NewGuid(),[]),ai,default)) { }
    }
    [Fact] public async Task PreparedClientFreezesKeyAndRetainsSeparateReasoning()
    {
        using var http = new HttpClient(new FakeHttpHandler(async (r,ct) =>
        {
            Assert.Equal("old-key",r.Headers.Authorization?.Parameter);
            using var json = JsonDocument.Parse(await r.Content!.ReadAsStringAsync(ct));
            Assert.Equal("previous reasoning",json.RootElement.GetProperty("messages")[0].GetProperty("reasoning_content").GetString());
            return Reply("data: {\"choices\":[{\"delta\":{\"reasoning_content\":\"internal\"}}]}\n\n");
        }));
        var secrets = new InMemorySecretStore(); await secrets.SetAsync("ai","old-key",default);
        IAiClient original = new ChatCompletionsClient(http,secrets,new StubServices());
        var prepared = await original.PrepareAsync(new(),default); await secrets.SetAsync("ai","new-key",default);
        var chunks = new List<AiChunk>();
        await foreach(var chunk in prepared.StreamAsync(new(Guid.NewGuid(),[new(MessageRole.Assistant,"previous answer",[],"previous reasoning")]),new(),default)) chunks.Add(chunk);
        Assert.Equal("internal",string.Concat(chunks.Select(c => c.ReasoningContent)));
        Assert.Equal("answer",string.Concat(chunks.Select(c => c.Text)));
    }
    [Fact] public async Task ErrorIncludesStatusAndProviderCodeButRedactsCredential()
    {
        using var http = new HttpClient(new FakeHttpHandler((_,_) => Task.FromResult(FakeHttpHandler.Json("{\"error\":{\"code\":\"invalid_model\",\"message\":\"bad model for test-key\"}}",HttpStatusCode.BadRequest))));
        var secrets = new InMemorySecretStore(); await secrets.SetAsync("ai","test-key",default);
        var error = await Assert.ThrowsAsync<ServiceFailure>(async () => { await foreach(var _ in new ChatCompletionsClient(http,secrets,new StubServices()).StreamAsync(new(Guid.NewGuid(),[]),new(),default)) { } });
        Assert.Contains("400",error.Message); Assert.Contains("invalid_model",error.Message); Assert.DoesNotContain("test-key",error.Message);
    }
    [Fact] public async Task ServiceTestsUseTheSameNonQueuedGenerationLease()
    {
        var gate = new AiGenerationGate(); using var active = gate.TryAcquire(Guid.NewGuid());
        using var handler = new FakeHttpHandler((_,_) => throw new InvalidOperationException("network must not start"));
        using var http = new HttpClient(handler); var verification = new ServiceVerification(new byte[32]);
        var runner = new ExamHelper.Windows.Settings.ServiceTestRunner(http,new InMemorySecretStore(),verification,[1],gate);
        var result = await runner.TestTextAsync(new(),"test-key",default);
        Assert.False(result.Passed); Assert.Equal(0,handler.Calls); Assert.True(gate.IsBusy);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task OldCredentialCannotBeRetiredWhileItsRequestIsStillPreparing(bool deleteProfile)
    {
        var raw=new DelayedSecrets();ISecretStore secrets=new ExamHelper.Core.Settings.SynchronizedSecretStore(raw);
        await secrets.SetAsync("ai","old-key",default);await secrets.SetAsync("replacement","new-key",default);
        using var http=new HttpClient(new FakeHttpHandler((request,_) => { Assert.Equal("old-key",request.Headers.Authorization?.Parameter);return Task.FromResult(Reply()); }));
        await using var rig=new ConversationWorkspaceRig();rig.ClientOverride=new ChatCompletionsClient(http,secrets,new StubServices());
        var runtime=await rig.Workspace.NewAsync(false,default);await runtime.UpdateDraftTextAsync("question",default);
        var sending=runtime.ExecuteAsync(ExamHelper.Core.Interaction.AppCommand.Send,default);await raw.Started.Task;
        rig.Settings=deleteProfile ? new AppSettings { AiProfiles=[] } : rig.Settings with { Ai=rig.Settings.Ai with { SecretId="replacement",Model="new-model" } };
        var cleanup=secrets.SetAsync("ai","",default);var completedBeforePreparation=cleanup.IsCompleted;raw.Release.TrySetResult();
        await sending;await cleanup;await runtime.WaitForIdleAsync(default);
        Assert.False(completedBeforePreparation);Assert.Equal("answer",runtime.State.Messages[^1].Text);Assert.Equal("",await raw.GetAsync("ai",default));
    }
    private sealed class DelayedSecrets : ISecretStore
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string,string> _values=new();private bool _delayed;
        public TaskCompletionSource Started { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);public TaskCompletionSource Release { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task SetAsync(string id,string secret,CancellationToken ct) { _values[id]=secret;return Task.CompletedTask; }
        public async Task<string?> GetAsync(string id,CancellationToken ct) { if(id=="ai" && !_delayed) { _delayed=true;Started.TrySetResult();await Release.Task.WaitAsync(ct); }return _values.GetValueOrDefault(id); }
    }
    private static HttpResponseMessage Reply(string prefix = "") => new(HttpStatusCode.OK) { Content = new StringContent(prefix+"data: {\"choices\":[{\"delta\":{\"content\":\"answer\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") };
}
