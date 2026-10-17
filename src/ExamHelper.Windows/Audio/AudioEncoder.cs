using System.IO;
using ExamHelper.Core.Common;
using NAudio.Wave;
namespace ExamHelper.Windows.Audio;
public static class AudioEncoder
{
    public static void EncodeToPcm16(string source, string destination)
    {
        using var reader = new WaveFileReader(source);
        if (reader.Length == 0) throw new ServiceFailure(FailureCategory.InvalidPayload, "没有录到有效音频。");
        try
        {
            using var resampler = new MediaFoundationResampler(reader, new WaveFormat(16000, 16, 1)) { ResamplerQuality = 60 };
            WaveFileWriter.CreateWaveFile(destination, resampler);
            using var output = new WaveFileReader(destination);
            if (output.Length == 0) throw new ServiceFailure(FailureCategory.InvalidPayload, "没有录到有效音频。");
        }
        catch { if (File.Exists(destination)) File.Delete(destination); throw; }
    }
}