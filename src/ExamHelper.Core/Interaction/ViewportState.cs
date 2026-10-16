namespace ExamHelper.Core.Interaction;
public sealed class ViewportState
{
    public bool FollowOutput { get; private set; } = true;
    public void Observe(double offset, double extent, double viewport) => FollowOutput = extent - viewport - offset <= 2;
}
