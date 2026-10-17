using System.Windows.Threading;
using ExamHelper.Core.Drafts;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.App.Overlay;
public sealed class OverlayHost : IOverlayHost
{
    private readonly SessionCoordinator _coordinator;
    private readonly IImageStore _images;
    private readonly Func<AppSettings> _settings;
    private readonly Func<UiSettings,Task> _saveUi;
    private readonly Func<Task> _home,_exit;
    private readonly Action<string> _report;
    private readonly IWindowProtectionApi? _protection;
    private readonly IHotkeyApi? _hotkeys;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    public OverlayWindow? CurrentWindow { get; private set; }
    public OverlayHost(SessionCoordinator coordinator,IImageStore images,Func<AppSettings> settings,Func<UiSettings,Task> saveUi,
        Func<Task> home,Func<Task> exit,IWindowProtectionApi? protection = null,IHotkeyApi? hotkeys = null,Action<string>? report = null)
    {
        _coordinator = coordinator; _images = images; _settings = settings; _saveUi = saveUi; _home = home; _exit = exit;
        _protection = protection; _hotkeys = hotkeys; _report = report ?? (_ => { });
        coordinator.UiCommandRequested += command => _dispatcher.BeginInvoke(() => _ = HandleUiAsync(command));
    }
    public Task<ProtectionResult> OpenAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); Close();
        var viewModel = new OverlayViewModel(_coordinator,_images);
        try
        {
            CurrentWindow = new(viewModel,_settings(),_protection,_hotkeys);
            if(CurrentWindow.Protection.Success && CurrentWindow.InitializationError is { } error) { Close(); throw new InvalidOperationException(error); }
            return Task.FromResult(CurrentWindow.Protection);
        }
        catch { viewModel.Dispose(); throw; }
    }
    public void Show()
    {
        var window = CurrentWindow ?? throw new InvalidOperationException("辅助窗口尚未初始化。");
        if(!window.EnsureProtection().Success) throw new InvalidOperationException("显示前捕获保护复核失败，已取消进入。");
        window.Show();
    }
    public void Close()
    {
        var window = CurrentWindow; CurrentWindow = null; window?.CloseFromHost();
    }
    private async Task HandleUiAsync(AppCommand command)
    {
        try
        {
            var window = CurrentWindow; if(window is null) return;
            switch(command)
            {
                case AppCommand.HideOverlay: window.HideWithRecovery(); break;
                case AppCommand.ToggleVisibility: if(window.IsVisible) window.HideWithRecovery(); else Show(); break;
                case AppCommand.ToggleCollapse: window.ToggleCollapse(); break;
                case AppCommand.ReturnHome: case AppCommand.Exit:
                    try { await _saveUi(window.CaptureGeometry()); } catch(Exception error) { _report(Common.ObservableObject.Explain(error)); }
                    Close();
                    if(command == AppCommand.ReturnHome) await _home(); else await _exit();
                    break;
            }
        }
        catch(Exception error)
        {
            try { await _coordinator.AbortEntryAsync(CancellationToken.None); } catch { }
            Close(); await _home(); _report(Common.ObservableObject.Explain(error));
        }
    }
}