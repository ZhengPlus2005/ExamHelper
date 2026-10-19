using System.IO;
using System.Windows;
namespace ExamHelper.InteractionLab;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length >= 2 && (e.Args[0] == "--verify" || e.Args[0] == "--verify-capture-control" || e.Args[0] == "--verify-audio" || e.Args[0] == "--verify-product" || e.Args[0] == "--verify-tray" || e.Args[0] == "--verify-home" || e.Args[0] == "--verify-displays" || e.Args[0] == "--demo-home"))
        {
            try { if(e.Args[0]=="--demo-home") { await HomeInteractionVerification.RunAsync(Path.GetFullPath(e.Args[1]),true);return; } if(e.Args[0]=="--verify-home") await HomeInteractionVerification.RunAsync(Path.GetFullPath(e.Args[1]));else if(e.Args[0]=="--verify-displays") await DisplayVerification.RunAsync(Path.GetFullPath(e.Args[1]));else if(e.Args[0] == "--verify-tray") await TrayVerification.RunAsync(Path.GetFullPath(e.Args[1])); else if (e.Args[0] == "--verify-audio") await AudioVerification.RunAsync(Path.GetFullPath(e.Args[1])); else if(e.Args[0] == "--verify-capture-control") { await CaptureVerification.RunAsync(Path.Combine(Path.GetFullPath(e.Args[1]),"enabled"),true,true); await CaptureVerification.RunAsync(Path.Combine(Path.GetFullPath(e.Args[1]),"disabled"),true,false); } else await CaptureVerification.RunAsync(Path.GetFullPath(e.Args[1]),e.Args[0] == "--verify-product"); Shutdown(0); }
            catch (Exception ex) { Directory.CreateDirectory(e.Args[1]); File.WriteAllText(Path.Combine(e.Args[1], "error.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var window = new MainWindow();
        if (!window.Protection.Success) { MessageBox.Show($"保护初始化失败：{window.Protection.ErrorCode}"); Shutdown(1); return; }
        MainWindow = window;
        window.Closed += (_, _) => Shutdown();
        window.Show();
    }
}
