using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using ExamHelper.App.Controls;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.InteractionLab;
public partial class MainWindow : Window
{
    public ProtectionResult Protection { get; }
    private readonly WindowInteractionSession _interaction;
    private readonly HoverTextInputBehavior _input;
    private readonly HotkeyRegistry _hotkeys;
    private readonly HwndSource _source;
    private bool _collapsed;
    private int _count;
    public MainWindow(bool registerHotkeys = true)
    {
        InitializeComponent();
        var hwnd = new WindowInteropHelper(this).EnsureHandle();
        Protection = new WindowProtection().ApplyAndVerify(hwnd);
        ProtectionStatus.Text = $"捕获保护读回：0x{Protection.EffectiveAffinity:X}；成功：{Protection.Success}";
        AnswerText.Text = string.Join("\n\n", Enumerable.Range(1, 60).Select(i => $"第 {i} 段 · 这是用于检查长回答滚动的内容。鼠标停在滚动条滑块一秒后，上下移动；停稳一秒固定。"));
        _interaction = new WindowInteractionSession(this);
        _ = new WindowManipulationBehavior(this, MoveHandle, ResizeHandle, _interaction);
        _ = new HoverScrollBehavior(AnswerScroll, _interaction);
        _input = new HoverTextInputBehavior(this, Editor);
        _input.StatusChanged += message => Status.Text = message;
        _hotkeys = new HotkeyRegistry(hwnd);
        _hotkeys.EscapePressed += _interaction.End;
        _hotkeys.CommandReceived += Execute;
        _interaction.ActiveChanged += active =>
        {
            if (!_hotkeys.SetManipulationEscape(active) && active) { _interaction.End(); Status.Text = "Esc 已被占用，本次移动操作已取消。"; return; }
            Status.Text = active ? $"正在 {_interaction.Mode}；停稳一秒或按 Esc 固定" : "已固定";
        };
        _source = HwndSource.FromHwnd(hwnd); _source.AddHook(HotkeyHook);
        if (registerHotkeys)
        {
            var report = _hotkeys.Register(HotkeyBinding.Defaults);
            Status.Text = report.Success ? "快捷键已注册，Ctrl+Alt+F9 隐藏／显示。" : string.Join("；", report.Conflicts.Select(c => $"{c.Binding}：{c.ErrorCode}"));
            HideButton.IsEnabled = report.Success;
        }
        Bind(CountButton, () => Execute(AppCommand.CaptureToDraft));
        Bind(LayoutButton, () => { CountButton.Visibility = CountButton.IsVisible ? Visibility.Collapsed : Visibility.Visible; DwellBehavior.InvalidateWindow(this); });
        Bind(FoldButton, () => Execute(AppCommand.ToggleCollapse)); Bind(HideButton, () => Execute(AppCommand.ToggleVisibility));
        Bind(CloseButton, () => Execute(AppCommand.Exit)); Bind(CancelButton, () => Execute(AppCommand.CancelInput));
        Bind(ClearButton, () => Execute(AppCommand.ClearInput)); Bind(InsertChinese, () => Editor.AppendText("模拟识别：请用 C++ 回答。\n"));
        Editor.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) { Execute(AppCommand.Send); e.Handled = true; } };
        Closed += (_, _) => { _hotkeys.Dispose(); _input.Dispose(); _interaction.Dispose(); if (!_source.IsDisposed) _source.RemoveHook(HotkeyHook); };
    }
    private static void Bind(System.Windows.Controls.Button button, Action action)
    {
        var command = new DelegateCommand(action); button.Command = command; DwellBehavior.SetCommand(button, command);
    }
    private nint HotkeyHook(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == 0x0312) handled = _hotkeys.HandleMessage(wParam.ToInt32()); return 0;
    }
    private void Execute(AppCommand command)
    {
        switch (command)
        {
            case AppCommand.Exit: case AppCommand.ReturnHome: Close(); break;
            case AppCommand.ToggleVisibility: if (IsVisible) Hide(); else { Show(); DwellBehavior.InvalidateWindow(this); } break;
            case AppCommand.CancelInput: _input.CancelInput(); break;
            case AppCommand.ClearInput: Editor.Clear(); break;
            case AppCommand.ToggleCollapse:
                _input.CancelInput(); _interaction.End(); _collapsed = !_collapsed;
                AnswerScroll.Visibility = EditPanel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
                MinHeight = _collapsed ? 180 : 360; Height = _collapsed ? 220 : 720; DwellBehavior.InvalidateWindow(this); break;
            default: Status.Text = $"验证命令 {command}：{++_count} 次（不发送截图或网络请求）"; break;
        }
    }
}
