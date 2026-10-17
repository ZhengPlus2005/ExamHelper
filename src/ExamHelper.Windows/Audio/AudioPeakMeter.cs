using System.Buffers.Binary;
using NAudio.Wave;
namespace ExamHelper.Windows.Audio;
public static class AudioPeakMeter
{
    private static readonly Guid FloatFormat = new("00000003-0000-0010-8000-00aa00389b71");
    private static readonly Guid PcmFormat = new("00000001-0000-0010-8000-00aa00389b71");
    public static float Measure(ReadOnlySpan<byte> data,WaveFormat format)
    {
        var floating = format.Encoding == WaveFormatEncoding.IeeeFloat ||
            format is WaveFormatExtensible ext && ext.SubFormat == FloatFormat;
        var pcm = format.Encoding == WaveFormatEncoding.Pcm ||
            format is WaveFormatExtensible extended && extended.SubFormat == PcmFormat;
        var bytes = format.BitsPerSample / 8;
        if((!floating && !pcm) || bytes is < 1 or > 8) return 0;
        double peak = 0;
        for(int i = 0;i + bytes <= data.Length;i += bytes)
        {
            var sample = data.Slice(i,bytes);
            double value;
            if(floating)
                value = bytes == 4 ? BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(sample)) :
                    bytes == 8 ? BitConverter.Int64BitsToDouble(BinaryPrimitives.ReadInt64LittleEndian(sample)) : 0;
            else
                value = bytes switch
                {
                    1 => (sample[0] - 128) / 128d,
                    2 => BinaryPrimitives.ReadInt16LittleEndian(sample) / 32768d,
                    3 => ((sample[0] | sample[1] << 8 | sample[2] << 16) << 8 >> 8) / 8388608d,
                    4 => BinaryPrimitives.ReadInt32LittleEndian(sample) / 2147483648d,
                    _ => 0
                };
            if(double.IsFinite(value)) peak = Math.Max(peak,Math.Abs(value));
        }
        return (float)Math.Clamp(peak,0,1);
    }
    public static float ToDisplayLevel(float peak)
    {
        if(!float.IsFinite(peak) || peak <= 0) return 0;
        return (float)Math.Clamp((20 * Math.Log10(peak) + 60) / 60,0,1);
    }
}
