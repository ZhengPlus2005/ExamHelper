using System.Globalization;
namespace ExamHelper.Core.Common;
public static class DisplayTime
{
    public static DateTimeOffset ToUtc8(DateTimeOffset value) => value.ToOffset(TimeSpan.FromHours(8));
    public static string Format(DateTimeOffset value,string format="yyyy-MM-dd HH:mm:ss") => ToUtc8(value).ToString(format,CultureInfo.InvariantCulture);
}
