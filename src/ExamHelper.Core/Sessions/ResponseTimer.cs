namespace ExamHelper.Core.Sessions;
public sealed record ResponseTiming(Guid RequestId,int Attempt,DateTimeOffset StartedAt,TimeSpan Elapsed,bool IsRunning);
public sealed class ResponseTimer
{
    private readonly object _sync=new();
    private readonly TimeProvider _clock;
    private readonly Guid _requestId;
    private readonly int _attempt;
    private readonly DateTimeOffset _startedAt;
    private readonly long _stamp;
    private ResponseTiming? _finished;
    public ResponseTimer(TimeProvider clock,Guid requestId,int attempt)
    { _clock=clock; _requestId=requestId; _attempt=attempt; _startedAt=clock.GetUtcNow(); _stamp=clock.GetTimestamp(); }
    private ResponseTiming Capture(bool running)=>new(_requestId,_attempt,_startedAt,_clock.GetElapsedTime(_stamp,_clock.GetTimestamp()),running);
    public ResponseTiming Snapshot() { lock(_sync) return _finished ?? Capture(true); }
    public ResponseTiming Finish() { lock(_sync) return _finished ??= Capture(false); }
}
