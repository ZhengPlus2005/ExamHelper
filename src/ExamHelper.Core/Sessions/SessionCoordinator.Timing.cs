using System.Collections.Immutable;
namespace ExamHelper.Core.Sessions;
public sealed partial class SessionCoordinator
{
    private ResponseTimer? _responseTimer;
    public SessionSnapshot SnapshotForPresentation()
    {
        var snapshot=Volatile.Read(ref _state); var timer=Volatile.Read(ref _responseTimer);
        return timer is null ? snapshot : ApplyTiming(snapshot,timer.Snapshot());
    }
    private static SessionSnapshot ApplyTiming(SessionSnapshot snapshot,ResponseTiming timing)=>snapshot with
    {
        Messages=snapshot.Messages.Select(m=>m.Role==MessageRole.Assistant && m.RequestId==timing.RequestId && m.Timing?.Attempt==timing.Attempt ? m with { Timing=timing } : m).ToImmutableArray()
    };
    private void FinishTimingLocked(RequestSnapshot request)
    {
        if(_responseTimer is not { } timer) return;
        var current=timer.Snapshot();
        if(current.RequestId!=request.RequestId || current.Attempt!=request.Attempt) return;
        _state=ApplyTiming(_state,timer.Finish()); _responseTimer=null;
    }
}
