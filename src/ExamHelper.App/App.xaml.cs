using System.Windows;
namespace ExamHelper.App;
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var home = new Window { Title = "ExamHelper — 开发中", Width = 800, Height = 520,
            Content = new System.Windows.Controls.TextBlock { Text = "主页开发中。核心交互请运行独立 InteractionLab。", Margin = new Thickness(32) } };
        home.Closed += (_, _) => Shutdown();
        home.Show();
    }
}
