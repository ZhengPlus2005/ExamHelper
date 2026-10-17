using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public sealed class StubServices : IAiClient,IAsrClient,IMicrophoneRecorder,IScreenCapture,IImageStore
{
    public IAsyncEnumerable<AiChunk> StreamAsync(AiRequest r,AiSettings s,CancellationToken ct) => throw new NotSupportedException();
    public Task<string> TranscribeAsync(RecordedAudio a,AsrSettings s,CancellationToken ct) => Task.FromResult("测试");
    public Task StartAsync(string id,CancellationToken ct) => Task.CompletedTask;
    public Task<RecordedAudio> StopAsync(CancellationToken ct) => Task.FromResult(new RecordedAudio(Guid.NewGuid(),"test.wav",TimeSpan.FromSeconds(1)));
    public Task CancelAsync() => Task.CompletedTask;
    public Task<CapturedImage> CaptureAsync(CancellationToken ct) => Task.FromResult(new CapturedImage([1],1,1));
    public Task<ImageRef> AddAsync(CapturedImage image,CancellationToken ct) => Task.FromResult(new ImageRef(Guid.NewGuid(),"test.png",image.Width,image.Height));
    public Task<byte[]> ReadAsync(ImageRef image,CancellationToken ct) => Task.FromResult<byte[]>([1]);
    public Task DeleteUnreferencedAsync(IReadOnlySet<Guid> liveIds,CancellationToken ct) => Task.CompletedTask;
}