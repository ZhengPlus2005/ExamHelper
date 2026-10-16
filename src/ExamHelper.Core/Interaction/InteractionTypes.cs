namespace ExamHelper.Core.Interaction;
public readonly record struct PointerSample(double X, double Y, long Timestamp);
public enum InteractionMode { Idle, Dwell, Moving, Resizing, Scrolling }
