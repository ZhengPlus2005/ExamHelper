using ExamHelper.Core.Ai;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public sealed class ConversationWorkspaceRig : IAsyncDisposable
{
    public DeferredAiClient Ai { get; } = new();
    public InMemorySessionStore Store { get; } = new();
    public AiGenerationGate Generation { get; } = new();
    public ManualTimeProvider Clock { get; } = new();
    public AppSettings Settings { get; set; } = new();
    public ConversationWorkspace Workspace { get; }
    public IAsrClient Asr { get; set; } = new StubServices();
    public ConversationWorkspaceRig()
    {
        Workspace = new(Store,() => { var stub = new StubServices(); return new(Ai,Asr,stub,stub,stub,() => Settings,Store,Clock,Generation); });
    }
    public ValueTask DisposeAsync() => Workspace.DisposeAsync();
}
