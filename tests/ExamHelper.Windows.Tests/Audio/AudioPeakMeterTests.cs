using System.Buffers.Binary;
using ExamHelper.Windows.Audio;
using NAudio.Wave;
using Xunit;
namespace ExamHelper.Windows.Tests.Audio;
public class AudioPeakMeterTests
{
    [Theory]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    public void PcmInterleavedSamplesMeasureActualMagnitude(int bits)
    {
        var data = bits switch
        {
            8 => new byte[] {128,192},
            16 => new byte[] {0,0,0,64},
            24 => new byte[] {0,0,0,0,0,64},
            _ => new byte[] {0,0,0,0,0,0,0,64}
        };
        Assert.Equal(0.5f,AudioPeakMeter.Measure(data,new WaveFormat(48000,bits,2)),3);
    }
    [Fact]
    public void FloatAndExtensibleSamplesAndSilenceAreHandled()
    {
        var data = new byte[8]; BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(4),BitConverter.SingleToInt32Bits(-0.25f));
        Assert.Equal(0.25f,AudioPeakMeter.Measure(data,WaveFormat.CreateIeeeFloatWaveFormat(48000,2)),3);
        Assert.Equal(0.25f,AudioPeakMeter.Measure(data,new WaveFormatExtensible(48000,32,2)),3);
        Assert.Equal(0,AudioPeakMeter.Measure(new byte[16],new WaveFormat(16000,16,1)));
    }
    [Fact]
    public void DisplayMappingIsVisibleMonotonicAndFinite()
    {
        Assert.Equal(0,AudioPeakMeter.ToDisplayLevel(0));
        Assert.Equal(0,AudioPeakMeter.ToDisplayLevel(float.NaN));
        Assert.InRange(AudioPeakMeter.ToDisplayLevel(0.01f),0.30f,0.35f);
        Assert.True(AudioPeakMeter.ToDisplayLevel(0.1f) > AudioPeakMeter.ToDisplayLevel(0.01f));
        Assert.Equal(1,AudioPeakMeter.ToDisplayLevel(2));
    }
}
