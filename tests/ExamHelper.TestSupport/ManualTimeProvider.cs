namespace ExamHelper.TestSupport;
public sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
    public void Advance(TimeSpan elapsed) => _ticks += elapsed.Ticks;
}
