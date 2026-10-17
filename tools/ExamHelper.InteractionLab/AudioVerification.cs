using System.IO;
using System.Text.Json;
using ExamHelper.Core.Audio;
using ExamHelper.Windows.Audio;
using NAudio.Wave;
namespace ExamHelper.InteractionLab;
internal static class AudioVerification
{
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        var root = Path.Combine(directory, "TempAudio");
        var devices = MicrophoneRecorder.GetDevices();
        await using var microphone = new MicrophoneRecorder(root);
        await using var controller = new RecordingController(microphone, TimeProvider.System);
        string? clipPath = null;
        try
        {
            await controller.StartAsync("", CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(2));
            var clip = await controller.StopAsync(CancellationToken.None);
            clipPath = clip.TemporaryPath;
            using var wave = new WaveFileReader(clip.TemporaryPath);
            var passed = wave.Length > 0 && wave.WaveFormat.SampleRate == 16000 &&
                wave.WaveFormat.Channels == 1 && wave.WaveFormat.BitsPerSample == 16;
            wave.Dispose();
            await controller.CancelAsync();
            File.WriteAllText(Path.Combine(directory, "audio-result.json"), JsonSerializer.Serialize(new
            {
                Passed = passed && !File.Exists(clipPath),
                DeviceNames = devices.Select(d => d.Name), SampleRate = 16000, Channels = 1, BitsPerSample = 16,
                DurationSeconds = clip.Duration.TotalSeconds, DeletedAfterTest = !File.Exists(clipPath),
                Uploads = 0, TestKind = "default microphone, real manual start/stop, two seconds; no transcription"
            }, new JsonSerializerOptions { WriteIndented = true }));
            if (!passed) throw new InvalidOperationException("麦克风测试未产生有效格式的采样。");
        }
        catch (Exception error)
        {
            await controller.CancelAsync();
            File.WriteAllText(Path.Combine(directory, "audio-result.json"), JsonSerializer.Serialize(new
            {
                Passed = false, DeviceNames = devices.Select(d => d.Name), Error = error.Message,
                DeletedAfterTest = clipPath is null || !File.Exists(clipPath), Uploads = 0
            }, new JsonSerializerOptions { WriteIndented = true }));
            throw;
        }
    }
}