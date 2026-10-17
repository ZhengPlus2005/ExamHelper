using System.Windows;
using ExamHelper.App.Composition;
using ExamHelper.App.Home;
namespace ExamHelper.App;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e); ShutdownMode = ShutdownMode.OnExplicitShutdown;
        try
        {
            var services = await AppComposition.CreateAsync();
            var home = new HomeWindow(services); MainWindow = home; home.Show();
        }
        catch(Exception error) { MessageBox.Show(Common.ObservableObject.Explain(error),"ExamHelper 启动失败"); Shutdown(1); }
    }
}
