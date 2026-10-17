using System.Net.Http;
using ExamHelper.App.Settings;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public class SettingsConcurrencyTests
{
    [Fact]
    public async Task AutomaticPromptSaveCannotOverwriteNewerProviderSettings()
    {
        var store = new DelayedStore(); var secrets = new InMemorySecretStore(); var verification = new ServiceVerification(new byte[32]);
        using var http = new HttpClient(new FakeHttpHandler((_,_) => throw new NotSupportedException()));
        await using var recording = new RecordingController(new StubServices(),new ManualTimeProvider());
        await using var vm = new SettingsViewModel(new(),"ai","asr",store,secrets,verification,new(http,secrets,verification,[1]),recording,_ => { });
        vm.DirectCapturePrompt = "自动保存的提示词";
        var automatic = vm.AutoSavePromptAsync(default); await store.FirstStarted.Task;
        vm.AiModel = "new-model";
        var explicitSave = vm.SaveAsync(default);
        store.ReleaseFirst.TrySetResult();
        await Task.WhenAll(automatic,explicitSave);
        Assert.Equal("new-model",store.Value.Ai.Model);
        Assert.Equal("自动保存的提示词",store.Value.DirectCapturePrompt);
    }
    private sealed class DelayedStore : ISettingsStore
    {
        private int _calls;
        public AppSettings Value { get; private set; } = new();
        public TaskCompletionSource FirstStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseFirst { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<AppSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Value);
        public async Task SaveAsync(AppSettings value,CancellationToken ct)
        {
            if(Interlocked.Increment(ref _calls) == 1) { FirstStarted.TrySetResult(); await ReleaseFirst.Task.WaitAsync(ct); }
            Value = value;
        }
    }
}