using System.IO;
using Forms=System.Windows.Forms;
using Drawing=System.Drawing;
namespace ExamHelper.App.Lifecycle;
public sealed class TrayIconService : ITrayIconService
{
    private readonly Forms.NotifyIcon _notify;
    private readonly Forms.ContextMenuStrip _menu;
    private readonly Drawing.Icon _icon;
    private readonly Stream _iconStream;
    private readonly TrayActivationDebounce _clicks=new(TimeProvider.System,TimeSpan.FromMilliseconds(Forms.SystemInformation.DoubleClickTime));
    private bool _disposed;
    public bool IsVisible => !_disposed && _notify.Visible;
    public event Action? Activated;
    public event Action? ExitRequested;
    public TrayIconService()
    {
        _iconStream=System.Windows.Application.GetResourceStream(new Uri("/ExamHelper.App;component/Assets/app-icon.ico",UriKind.Relative)).Stream;
        _icon=new Drawing.Icon(_iconStream,Forms.SystemInformation.SmallIconSize);
        _menu=new Forms.ContextMenuStrip(); var exit=new Forms.ToolStripMenuItem("结束进程"); exit.Click+=(_,_) => ExitRequested?.Invoke(); _menu.Items.Add(exit);
        _notify=new Forms.NotifyIcon { Icon=_icon,Text="ExamHelper · 单击显示主页",ContextMenuStrip=_menu,Visible=true };
        _notify.MouseClick+=(_,e) => { if(e.Button==Forms.MouseButtons.Left && _clicks.TryActivate()) Activated?.Invoke(); };
    }
    public void Dispose()
    {
        if(_disposed) return; _disposed=true; _notify.Visible=false; _notify.Dispose(); _menu.Dispose(); _icon.Dispose(); _iconStream.Dispose();
    }
}
