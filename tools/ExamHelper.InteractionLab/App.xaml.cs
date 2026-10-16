using System.IO;
using System.Windows;
namespace ExamHelper.InteractionLab;
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 2 && e.Args[0] == "--verify")
        {
            try { await CaptureVerification.RunAsync(Path.GetFullPath(e.Args[1])); Shutdown(0); }
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
