using System.Security.Principal;
using System.Windows;
using ExamHelper.App.Composition;
using ExamHelper.App.Home;
using ExamHelper.Windows.Native;
namespace ExamHelper.App;
public partial class App : Application
{
    private Mutex? _instance;
    private bool _ownsMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        if(e.Args.Length == 2 && e.Args[0] == "--self-test")
        {
            Shutdown(await Common.StartupDiagnostics.RunAsync(e.Args[1])); return;
        }
        try
        {
            if(!WindowsPlatform.IsSupported(WindowsPlatform.Current()))
                throw new InvalidOperationException("仅支持 Windows 11 x64 工作站，不支持 Windows 10、ARM 或 Windows Server。");
            var identity = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
            _instance = new Mutex(true,"Local\\ExamHelper-" + identity,out _ownsMutex);
            if(!_ownsMutex) { MessageBox.Show("ExamHelper 已在运行。","ExamHelper"); Shutdown(); return; }
            var services = await AppComposition.CreateAsync();
            var home = new HomeWindow(services); MainWindow = home; home.Show();
        }
        catch(Exception error) { MessageBox.Show(Common.ObservableObject.Explain(error),"ExamHelper 启动失败"); Shutdown(1); }
    }
    protected override void OnExit(ExitEventArgs e)
    {
        if(_ownsMutex) _instance?.ReleaseMutex();
        _instance?.Dispose(); base.OnExit(e);
    }
}
