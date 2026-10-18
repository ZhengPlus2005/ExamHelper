using System.Globalization;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Common;
public static class ResponseTimingFormatter
{
    public static string Format(ResponseTiming? timing)=>timing is null?"":timing.IsRunning
        ?"已用 "+Math.Floor(timing.Elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture)+" 秒"
        :"用时 "+timing.Elapsed.TotalSeconds.ToString("F1",CultureInfo.InvariantCulture)+" 秒";
}
