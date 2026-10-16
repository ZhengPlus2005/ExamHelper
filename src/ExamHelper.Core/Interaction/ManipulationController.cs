namespace ExamHelper.Core.Interaction;
public readonly record struct ManipulationUpdate(double DeltaX, double DeltaY, bool IsFinished);
public sealed class ManipulationController(TimeProvider clock)
{
    private PointerSample _start, _anchor;
    private long _lastMovement;
    public InteractionMode Mode { get; private set; }
    public void Begin(InteractionMode mode, PointerSample start)
    {
        if (mode is not (InteractionMode.Moving or InteractionMode.Resizing or InteractionMode.Scrolling)) throw new ArgumentOutOfRangeException(nameof(mode));
        Mode = mode; _start = _anchor = start; _lastMovement = start.Timestamp;
    }
    public ManipulationUpdate Update(PointerSample sample)
    {
        if (Mode == InteractionMode.Idle) return new(0, 0, true);
        var dx = sample.X - _anchor.X; var dy = sample.Y - _anchor.Y;
        if (dx * dx + dy * dy > 9) { _anchor = sample; _lastMovement = sample.Timestamp; }
        var finished = clock.GetElapsedTime(_lastMovement, sample.Timestamp) >= TimeSpan.FromSeconds(1);
        var result = new ManipulationUpdate(Mode == InteractionMode.Scrolling ? 0 : sample.X - _start.X, sample.Y - _start.Y, finished);
        if (finished) End(); return result;
    }
    public void End() => Mode = InteractionMode.Idle;
}
public static class CoordinateTransform
{
    public static PointerSample ToDip(PointerSample pixels, double scaleX, double scaleY)
    {
        if (scaleX <= 0 || scaleY <= 0) throw new ArgumentOutOfRangeException(nameof(scaleX));
        return new(pixels.X / scaleX, pixels.Y / scaleY, pixels.Timestamp);
    }
}
