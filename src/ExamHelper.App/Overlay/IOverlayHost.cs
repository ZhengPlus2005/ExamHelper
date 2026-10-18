using ExamHelper.Windows.Native;
namespace ExamHelper.App.Overlay;
public interface IOverlayHost
{
    void Bind(ExamHelper.Core.Sessions.SessionCoordinator coordinator) { }
    Task<ProtectionResult> OpenAsync(CancellationToken ct);
    void Show();
    void Close();
}