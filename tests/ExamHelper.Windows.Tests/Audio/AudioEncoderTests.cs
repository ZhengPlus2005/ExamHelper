using System.IO;
using ExamHelper.Windows.Audio;
using Xunit;
namespace ExamHelper.Windows.Tests.Audio;
public class AudioEncoderTests
{
    [Fact]
    public void EncodesFloatStereo48kAs16kMono16BitPcm()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExamHelper-audio-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var source = Path.Combine(root, "source.wav"); var target = Path.Combine(root, "target.wav");
            using (var writer = new BinaryWriter(File.Create(source)))
            {
                var dataSize = 4800 * 2 * 4;
                writer.Write("RIFF"u8); writer.Write(36 + dataSize); writer.Write("WAVEfmt "u8);
                writer.Write(16); writer.Write((short)3); writer.Write((short)2); writer.Write(48000);
                writer.Write(48000 * 8); writer.Write((short)8); writer.Write((short)32);
                writer.Write("data"u8); writer.Write(dataSize);
                for (var i = 0; i < 4800; i++) { float sample = (float)(0.1 * Math.Sin(2 * Math.PI * 440 * i / 48000)); writer.Write(sample); writer.Write(sample); }
            }
            AudioEncoder.EncodeToPcm16(source, target);
            using var file = new BinaryReader(File.OpenRead(target));
            Assert.Equal("RIFF", new string(file.ReadChars(4)));
            file.BaseStream.Position = 20;
            Assert.Equal(1, file.ReadInt16()); Assert.Equal(1, file.ReadInt16());
            Assert.Equal(16000, file.ReadInt32()); Assert.Equal(32000, file.ReadInt32());
            Assert.Equal(2, file.ReadInt16()); Assert.Equal(16, file.ReadInt16());
            Assert.InRange(new FileInfo(target).Length, 3000, 3600);
        }
        finally { Directory.Delete(root, true); }
    }
}