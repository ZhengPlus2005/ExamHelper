using System.Collections.Immutable;
using ExamHelper.App.Controls;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ExamHelper.App.Overlay;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.InteractionLab;
// Synthetic content only. This development fixture is excluded from the installer.
internal sealed class ProductCaptureFixture(SessionCoordinator coordinator,OverlayWindow window) : IAsyncDisposable
{
    public OverlayWindow Window { get; } = window;
    public static async Task<ProductCaptureFixture> CreateAsync(PhysicalWindowPlacement? placement = null)
    {
        var services = new ReferenceServices();
        var verification=new ServiceVerification(new byte[32]);
        var endpoint=new AiSettings { BaseUri=new("https://example.invalid/v1"),SecretFingerprint="synthetic-digest" };
        var models=Enumerable.Range(0,18).Select(i => { var name=i==0 ? "kimi-k2.6" : $"reference-model-{i}"; return new AiModelProfile { Name=name,TextVerifiedFingerprint=verification.AiFingerprint(new() { Ai=endpoint with { Model=name } }),VerifiedFingerprint=verification.AiFingerprint(new() { Ai=endpoint with { Model=name } }) }; }).ToImmutableArray();
        var profile=new AiServiceProfile { Name="受控测试配置",Settings=endpoint,Models=models,DefaultModelId=models[0].Id };
        var settings=AiProfileCatalog.Normalize(new() { Ui=new() { Placement=placement },Hotkeys=new() { Bindings=[] },AiProfiles=[profile],ActiveAiProfileId=profile.Id });
        var coordinator = new SessionCoordinator(services,services,services,services,services,() => settings,services,TimeProvider.System,verification:verification);
        await coordinator.StartNewAsync(CancellationToken.None);
        await coordinator.AppendSpeechResultAsync(new(coordinator.State.Id,coordinator.State.SpeechGeneration,Guid.NewGuid(),"受控测试：请给出一个 C++ 示例。"),CancellationToken.None);
        await coordinator.ExecuteAsync(AppCommand.Send,CancellationToken.None);
        await coordinator.WaitForIdleAsync(CancellationToken.None);
        await coordinator.AppendSpeechResultAsync(new(coordinator.State.Id,coordinator.State.SpeechGeneration,Guid.NewGuid(),"这是只读语音草稿"),CancellationToken.None);
        var viewModel = new OverlayViewModel(coordinator,services,verification:verification);
        var overlay = new OverlayWindow(viewModel,settings);
        if(placement is null) { overlay.Left=120; overlay.Top=100; overlay.Width=640; overlay.Height=600; }
        return new(coordinator,overlay);
    }
    public void PrepareProtectedSelectors() { var selector=(ModelSelector)Window.FindName("AuxiliaryModels"); selector.OpenHoverMenu(ModelSelectorPart.Model); Window.UpdateLayout(); }
    public void SavePreview(string directory)
    {
        Window.UpdateLayout();
        var bitmap = new RenderTargetBitmap((int)Window.ActualWidth,(int)Window.ActualHeight,96,96,PixelFormats.Pbgra32);
        bitmap.Render(Window); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory,"product-overlay.png")); encoder.Save(file);
    }
    public async ValueTask DisposeAsync() { Window.CloseFromHost(); await coordinator.DisposeAsync(); }
    private sealed class ReferenceServices : IAiClient,IAsrClient,IMicrophoneRecorder,IScreenCapture,IImageStore,ISessionStore
    {
        private readonly Dictionary<Guid,SessionSnapshot> _saved = [];
        public async IAsyncEnumerable<AiChunk> StreamAsync(AiRequest r,AiSettings s,[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            await Task.CompletedTask;
            yield return new("C++ 示例：\n```cpp\n#include <iostream>\nint main() {\n    std::cout << \"Hello\";\n}\n```\n这段文字仅用于检查窗口和代码块。",false);
            yield return new("",true);
        }
        public Task<string> TranscribeAsync(RecordedAudio a,AsrSettings s,CancellationToken ct) => throw new NotSupportedException();
        public Task StartAsync(string d,CancellationToken ct) => throw new NotSupportedException();
        public Task<RecordedAudio> StopAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CancelAsync() => Task.CompletedTask;
        public Task<CapturedImage> CaptureAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<ImageRef> AddAsync(CapturedImage i,CancellationToken ct) => throw new NotSupportedException();
        public Task<byte[]> ReadAsync(ImageRef i,CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(SessionSnapshot s,CancellationToken ct) { _saved[s.Id] = s; return Task.CompletedTask; }
        public Task<SessionSnapshot?> LoadAsync(Guid id,CancellationToken ct) => Task.FromResult(_saved.GetValueOrDefault(id));
        public Task<IReadOnlyList<SessionSummary>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<SessionSummary>>([]);
        public Task DeleteAsync(Guid id,CancellationToken ct) { _saved.Remove(id); return Task.CompletedTask; }
    }
}