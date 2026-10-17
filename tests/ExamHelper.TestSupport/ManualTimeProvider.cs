namespace ExamHelper.TestSupport;
public sealed class ManualTimeProvider : TimeProvider
{
    private long _ticks;
    private readonly List<ManualTimer> _timers = [];
    public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    public override long GetTimestamp() => _ticks;
    public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(_ticks);
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(this, callback, state);
        _timers.Add(timer); timer.Change(dueTime, period); return timer;
    }
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        var target = checked(_ticks + elapsed.Ticks);
        while (_timers.Where(t => !t.Disposed && t.Due <= target).MinBy(t => t.Due) is { } timer)
        {
            _ticks = timer.Due;
            timer.Due = timer.Period > TimeSpan.Zero ? checked(_ticks + timer.Period.Ticks) : long.MaxValue;
            timer.Callback(timer.State);
        }
        _ticks = target;
    }
    private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
    {
        public TimerCallback Callback { get; } = callback;
        public object? State { get; } = state;
        public long Due { get; set; } = long.MaxValue;
        public TimeSpan Period { get; private set; }
        public bool Disposed { get; private set; }
        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            if (Disposed) return false;
            if (dueTime < Timeout.InfiniteTimeSpan || period < Timeout.InfiniteTimeSpan) throw new ArgumentOutOfRangeException(nameof(dueTime));
            Due = dueTime == Timeout.InfiniteTimeSpan ? long.MaxValue : checked(clock._ticks + dueTime.Ticks);
            Period = period; return true;
        }
        public void Dispose() { Disposed = true; Due = long.MaxValue; clock._timers.Remove(this); }
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }
}