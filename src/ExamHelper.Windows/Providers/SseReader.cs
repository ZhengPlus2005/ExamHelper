using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using ExamHelper.Core.Common;
namespace ExamHelper.Windows.Providers;
public static class SseReader
{
    public static async IAsyncEnumerable<string> ReadEventsAsync(Stream stream, [EnumeratorCancellation] CancellationToken ct)
    {
        using var reader = new StreamReader(stream,new UTF8Encoding(false,true),true,4096,true);
        var data = new StringBuilder();
        while (await reader.ReadLineAsync(ct).ConfigureAwait(false) is { } line)
        {
            if (line.Length == 0)
            {
                if (data.Length > 0) { yield return data.ToString(0,data.Length - 1); data.Clear(); }
                continue;
            }
            if (!line.StartsWith("data:",StringComparison.Ordinal)) continue;
            var value = line[5..]; if (value.StartsWith(' ')) value = value[1..];
            data.Append(value).Append('\n');
            if (data.Length > 16_000_000) throw new ServiceFailure(FailureCategory.InvalidPayload,"服务返回的单个流事件过大。");
        }
        // An event without its terminating blank line is incomplete.
    }
}