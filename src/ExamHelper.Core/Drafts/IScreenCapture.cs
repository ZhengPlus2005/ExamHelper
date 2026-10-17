namespace ExamHelper.Core.Drafts;
public sealed record CapturedImage(byte[] PngBytes, int Width, int Height);
public interface IScreenCapture { Task<CapturedImage> CaptureAsync(CancellationToken ct); }