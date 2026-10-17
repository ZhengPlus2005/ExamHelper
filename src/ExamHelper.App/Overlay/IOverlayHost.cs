using ExamHelper.Windows.Native;
namespace ExamHelper.App.Overlay;
public interface IOverlayHost
{
    Task<ProtectionResult> OpenAsync(CancellationToken ct);
    void Show();
    void Close();
}