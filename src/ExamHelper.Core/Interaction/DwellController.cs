namespace ExamHelper.Core.Interaction;
public sealed class DwellController(TimeProvider clock)
{
    private string? _target;
    private long _started;
    private TimeSpan _delay;
    private bool _blocked;
    public double Progress => _target is null || _blocked ? 0 : Math.Clamp(clock.GetElapsedTime(_started).TotalMilliseconds / _delay.TotalMilliseconds, 0, 1);
    public void Enter(string targetId, TimeSpan delay)
    {
        if (_blocked || _target is not null) return;
        if (delay <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delay));
        _target = targetId; _delay = delay; _started = clock.GetTimestamp();
    }
    public void Leave() { _target = null; _blocked = false; }
    public void Suspend() { _target = null; _blocked = true; }
    public void InvalidateLayout() => Suspend();
    public string? Tick()
    {
        if (_target is null || _blocked || clock.GetElapsedTime(_started) < _delay) return null;
        var result = _target; _target = null; _blocked = true; return result;
    }
}
