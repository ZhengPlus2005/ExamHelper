using System.IO;
namespace ExamHelper.Windows.Common;
public static class AtomicFile
{
    public static async Task WriteAsync(string path,ReadOnlyMemory<byte> bytes,CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,true))
            {
                await stream.WriteAsync(bytes,ct).ConfigureAwait(false); await stream.FlushAsync(ct).ConfigureAwait(false);
                stream.Flush(true);
            }
            ct.ThrowIfCancellationRequested(); File.Move(temporary,path,true);
        }
        finally { if(File.Exists(temporary)) File.Delete(temporary); }
    }
}