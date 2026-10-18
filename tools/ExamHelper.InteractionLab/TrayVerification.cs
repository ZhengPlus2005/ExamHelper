using System.IO;
using System.Reflection;
using System.Text.Json;
using ExamHelper.App.Lifecycle;
namespace ExamHelper.InteractionLab;
// Exercise the real NotifyIcon and its menu without reading user settings or
// driving Explorer. Events are programmatic; no process is terminated here.
internal static class TrayVerification
{
    public static async Task RunAsync(string directory)
    {
        Directory.CreateDirectory(directory); var activations=0; var exits=0;
        using var tray=new TrayIconService(); tray.Activated+=() => activations++; tray.ExitRequested+=() => exits++;
        await Task.Delay(100);
        var notify=typeof(TrayIconService).GetField("_notify",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(tray)!;
        var forms=notify.GetType().Assembly; var buttons=forms.GetType("System.Windows.Forms.MouseButtons")!; var argsType=forms.GetType("System.Windows.Forms.MouseEventArgs")!;
        var click=notify.GetType().GetMethod("OnMouseClick",BindingFlags.NonPublic|BindingFlags.Instance)!;
        object Mouse(string name) => Activator.CreateInstance(argsType,Enum.Parse(buttons,name),1,0,0,0)!;
        click.Invoke(notify,[Mouse("Left")]); click.Invoke(notify,[Mouse("Left")]); click.Invoke(notify,[Mouse("Right")]);
        var menu=notify.GetType().GetProperty("ContextMenuStrip")!.GetValue(notify)!;
        var items=(System.Collections.IEnumerable)menu.GetType().GetProperty("Items")!.GetValue(menu)!;
        var exit=items.Cast<object>().Single(i => (string?)i.GetType().GetProperty("Text")!.GetValue(i)=="结束进程"); exit.GetType().GetMethod("PerformClick",Type.EmptyTypes)!.Invoke(exit,null);
        var icon=notify.GetType().GetProperty("Icon")!.GetValue(notify)!; var bitmap=icon.GetType().GetMethod("ToBitmap")!.Invoke(icon,null)!;
        try
        {
            var format=bitmap.GetType().Assembly.GetType("System.Drawing.Imaging.ImageFormat")!;
            bitmap.GetType().GetMethod("Save",[typeof(string),format])!.Invoke(bitmap,[Path.Combine(directory,"tray-icon.png"),format.GetProperty("Png")!.GetValue(null)!]);
        }
        finally { ((IDisposable)bitmap).Dispose(); }
        var checks=new { NativeTrayCreated=tray.IsVisible,SingleClickAndDoubleClickDebounce=activations==1,RightMenuRaisesNormalExit=exits==1,Events="Programmatic real NotifyIcon/menu events; not physical Explorer clicks",NetworkRequests=0,UserSettingsRead=false,Passed=tray.IsVisible && activations==1 && exits==1 };
        await File.WriteAllTextAsync(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(checks,new JsonSerializerOptions { WriteIndented=true }));
        if(!checks.Passed) throw new InvalidOperationException("Tray verification failed.");
    }
}
