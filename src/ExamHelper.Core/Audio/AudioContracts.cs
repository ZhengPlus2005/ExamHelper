using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Audio;
public sealed record RecordedAudio(Guid OperationId, string TemporaryPath, TimeSpan Duration);
public interface IMicrophoneRecorder
{
    Task StartAsync(string deviceId, CancellationToken ct);
    Task<RecordedAudio> StopAsync(CancellationToken ct);
    Task CancelAsync();
}
public interface IAsrClient
{
    Task<string> TranscribeAsync(RecordedAudio audio, AsrSettings settings, CancellationToken ct);
}