using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using ExamHelper.App.Controls;
using ExamHelper.App.Settings;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.App.Overlay;
public partial class OverlayWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly AppSettings _settings;
    private readonly WindowProtection _protection;
    private readonly IHotkeyApi? _hotkeyApi;
    private readonly WindowInteractionSession _interaction;
    private HotkeyRegistry? _hotkeys;
    private NonActivatingWindowBehavior? _nonActivating;
    private HwndSource? _source;
    private bool _allowClose,_collapsed;
    private double _expandedHeight;
    public ProtectionResult Protection { get; private set; }
    public string? InitializationError { get; private set; }
    public OverlayWindow(OverlayViewModel viewModel,AppSettings settings,IWindowProtectionApi? protection = null,IHotkeyApi? hotkeys = null)
    {
        _viewModel = viewModel; _settings = settings; _protection = new(protection); _hotkeyApi = hotkeys;
        InitializeComponent(); DataContext = viewModel;
        var area = SystemParameters.WorkArea;
        MinWidth = Math.Min(480,area.Width); MinHeight = Math.Min(360,area.Height);
        Width = Math.Clamp(settings.Ui.Width,MinWidth,Math.Max(MinWidth,area.Width));
        Height = Math.Clamp(settings.Ui.Height,MinHeight,Math.Max(MinHeight,area.Height));
        Left = Math.Clamp(settings.Ui.Left ?? area.Right - Width - 24,area.Left,Math.Max(area.Left,area.Right - Width));
        Top = Math.Clamp(settings.Ui.Top ?? area.Top + 24,area.Top,Math.Max(area.Top,area.Bottom - Height));
        _expandedHeight = Height;
        Surface.Background = new SolidColorBrush(Color.FromArgb((byte)Math.Round(settings.Ui.Opacity * 255),24,32,45));
        _interaction = new(this);
        _ = new WindowManipulationBehavior(this,MoveHandle,ResizeHandle,_interaction);
        _ = new HoverScrollBehavior(AnswerScroll,_interaction);
        _ = new HoverScrollBehavior(DraftImages.ScrollViewer,_interaction);
        _ = new HoverScrollBehavior(DraftScroll,_interaction);
        _ = new HoverScrollBehavior(StatusScroll,_interaction);
        _interaction.ActiveChanged += active =>
        {
            if(_hotkeys?.SetManipulationEscape(active) == false && active) { _interaction.End(); _viewModel.ReportError("Esc 已被占用，本次移动操作已取消。"); }
        };
        SourceInitialized += OnSourceInitialized; new WindowInteropHelper(this).EnsureHandle();
        Bind(DirectButton,AppCommand.CaptureAndSend); Bind(CaptureButton,AppCommand.CaptureToDraft); Bind(RecordButton,AppCommand.ToggleRecording);
        Bind(NewButton,AppCommand.NewConversation); Bind(StopButton,AppCommand.StopAnswer); Bind(FoldButton,AppCommand.ToggleCollapse);
        Bind(HomeButton,AppCommand.ReturnHome); Bind(ExitButton,AppCommand.Exit); Bind(ClearButton,AppCommand.ClearInput); Bind(SendButton,AppCommand.Send);
        Bind(UndoButton,AppCommand.UndoSpeech); Bind(RetrySpeechButton,AppCommand.RetrySpeech); Bind(DiscardSpeechButton,AppCommand.DiscardSpeech);
        Bind(RetryRequestButton,AppCommand.RetryRequest); Bind(DiscardRequestButton,AppCommand.DiscardRequest);
        IsVisibleChanged += (_,_) => { viewModel.SetVisible(IsVisible); if(IsVisible) DwellBehavior.InvalidateWindow(this); };
        Closing += OnClosing;
        Closed += (_,_) =>
        {
            _interaction.Dispose(); _hotkeys?.Dispose(); _nonActivating?.Dispose(); _viewModel.Dispose();
            if(_source is { IsDisposed: false }) _source.RemoveHook(Hook);
        };
    }
    private void Bind(Button button,AppCommand command) { button.Command = _viewModel.Command(command); DwellBehavior.SetCommand(button,button.Command); }
    private void OnSourceInitialized(object? sender,EventArgs args)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        Protection = _protection.ApplyAndVerify(hwnd);
        if(!Protection.Success) { InitializationError = $"捕获保护初始化失败（{Protection.ErrorCode}）。"; return; }
        if(_source is { IsDisposed: false }) _source.RemoveHook(Hook);
        _hotkeys?.Dispose(); _nonActivating?.Dispose();
        _source = HwndSource.FromHwnd(hwnd); _source.AddHook(Hook);
        _nonActivating = new(this);
        _hotkeys = new(hwnd,_hotkeyApi);
        _hotkeys.EscapePressed += _interaction.End;
        _hotkeys.CommandReceived += command => _viewModel.Command(command).Execute(null);
        var report = _hotkeys.Register(_settings.Hotkeys.Bindings);
        if(!report.Success) InitializationError = "快捷键冲突：" + string.Join("；",report.Conflicts.Select(c => CommandNames.Get(c.Binding.Command) + " " + HotkeyEditor.Format(c.Binding)));
    }
    public ProtectionResult EnsureProtection()
    {
        Protection = _protection.ApplyAndVerify(new WindowInteropHelper(this).EnsureHandle()); return Protection;
    }
    public UiSettings CaptureGeometry() => _settings.Ui with { Left = Left,Top = Top,Width = Width,Height = _collapsed ? _expandedHeight : Height };
    public void ToggleCollapse()
    {
        _interaction.End();
        if(!_collapsed) { _expandedHeight = Height; _collapsed = true; Body.Visibility = Visibility.Collapsed; MinHeight = Math.Min(180,SystemParameters.WorkArea.Height); Height = Math.Min(240,SystemParameters.WorkArea.Height); }
        else { _collapsed = false; Body.Visibility = Visibility.Visible; MinHeight = Math.Min(360,SystemParameters.WorkArea.Height); Height = Math.Min(_expandedHeight,SystemParameters.WorkArea.Height); }
        DwellBehavior.InvalidateWindow(this);
    }
    public void CloseFromHost() { _allowClose = true; Close(); }
    private void OnClosing(object? sender,CancelEventArgs args)
    {
        if(_allowClose) return; args.Cancel = true; _viewModel.Command(AppCommand.Exit).Execute(null);
    }
    private nint Hook(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message == 0x0312 && _hotkeys is not null) handled = _hotkeys.HandleMessage(wParam.ToInt32());
        return 0;
    }
    private async void UndoEnter(object sender,System.Windows.Input.MouseEventArgs args) { try { await _viewModel.SetUndoHoverAsync(true); } catch { } }
    private async void UndoLeave(object sender,System.Windows.Input.MouseEventArgs args) { try { await _viewModel.SetUndoHoverAsync(false); } catch { } }
}