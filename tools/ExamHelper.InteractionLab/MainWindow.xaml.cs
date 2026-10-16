using System.Windows;
using System.Windows.Interop;
using ExamHelper.Windows.Native;
namespace ExamHelper.InteractionLab;
public partial class MainWindow : Window
{
    public ProtectionResult Protection { get; }
    public MainWindow()
    {
        InitializeComponent();
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        Protection = new WindowProtection().ApplyAndVerify(hwnd);
        ProtectionStatus.Text = $"捕获保护读回：0x{Protection.EffectiveAffinity:X}；成功：{Protection.Success}";
    }
    private void CloseClicked(object sender, RoutedEventArgs e) => Close();
}
