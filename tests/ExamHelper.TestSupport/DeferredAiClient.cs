using System.Runtime.CompilerServices;
using System.Threading.Channels;
using ExamHelper.Core.Ai;
using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public sealed class DeferredAiClient : IAiClient
{
    private readonly Queue<Attempt> _queued = [];
    public Attempt QueueAttempt(bool ignoreCancellation = false) { var attempt = new Attempt(ignoreCancellation); _queued.Enqueue(attempt); return attempt; }
    public async IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request,AiSettings settings,[EnumeratorCancellation] CancellationToken ct)
    {
        var attempt = _queued.Dequeue(); attempt.SettingsUsed = settings; attempt.Started.TrySetResult(request);
        await foreach(var chunk in attempt.Chunks.Reader.ReadAllAsync(attempt.IgnoreCancellation ? CancellationToken.None : ct)) yield return chunk;
    }
    public sealed class Attempt(bool ignoreCancellation)
    {
        public AiSettings? SettingsUsed { get; internal set; }
        public bool IgnoreCancellation { get; } = ignoreCancellation;
        public TaskCompletionSource<AiRequest> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<AiChunk> Chunks { get; } = Channel.CreateUnbounded<AiChunk>();
        public void Text(string text) => Chunks.Writer.TryWrite(new(text,false));
        public void Complete() { Chunks.Writer.TryWrite(new("",true)); Chunks.Writer.TryComplete(); }
        public void Fail(Exception error) => Chunks.Writer.TryComplete(error);
    }
}