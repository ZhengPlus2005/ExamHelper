using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ExamHelper.App.Composition;
using ExamHelper.App.Settings;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Images;
namespace ExamHelper.App.Home;
public partial class HomeWindow : Window
{
    private ExamHelper.App.Lifecycle.AppLifecycle? _lifecycle;
    private readonly HomeViewModel _viewModel;
    private readonly Func<Task<SettingsViewModel>> _settings;
    private readonly Func<Task> _shutdown;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromMilliseconds(75) };
    private SettingsWindow? _settingsWindow;
    private CancellationTokenSource? _selection;
    private bool _closing,_closePending,_syncSelection,_syncInput;
    private readonly HomeInputBehavior _inputBehavior;
    private readonly HomePasteBehavior _pasteBehavior;
    private DateTime _lastHistoryRefresh;
    public HomeDraftController DraftController { get; }
    public HomeWindow(AppComposition services) : this(services.Home,services.CreateSettingsViewModelAsync,
        async () => { await services.Workspace.ExitAsync(CancellationToken.None); await services.DisposeAsync(); },services.ImageImporter)
    { AttachLifecycle(services.StartLifecycle(new ExamHelper.App.Lifecycle.TrayIconService(),() => _settingsWindow?.TryCloseAsync() ?? Task.FromResult(true),() => DraftController.DrainAllAsync(CancellationToken.None))); }
    public HomeWindow(HomeViewModel viewModel,Func<Task<SettingsViewModel>> settings,Func<Task> shutdown,IImageImporter? importer = null,IClipboardAccess? clipboard = null)
    {
        _viewModel=viewModel; _settings=settings; _shutdown=shutdown;
        DraftController = new(() => viewModel.Workspace.Selected,importer ?? new ImageImportService(viewModel.ImageStore));
        InitializeComponent(); DataContext=viewModel; viewModel.Dispatch=action => Dispatcher.BeginInvoke(action);
        viewModel.VisibilityChanged += VisibilityChanged;
        HomeModels.SelectionRequested += async choice => { await RunAsync(() => viewModel.SelectModelAsync(choice,CancellationToken.None)); HomeModels.Configure(viewModel.SettingsSnapshot,viewModel.Workspace.Selected?.State.Selection,false,viewModel.Verification); };
        HomeModels.Failed += message => viewModel.Status=message;
        _inputBehavior = new HomeInputBehavior(HomeDraftInput,() => DraftController.SendAsync(CancellationToken.None),error => viewModel.Status=Common.ObservableObject.Explain(error));
        _pasteBehavior=new(HomeDraftInput,DraftController,clipboard ?? new WindowsClipboardAccess(),error=>viewModel.Status=Common.ObservableObject.Explain(error));
        Loaded += async (_,_) => { if(viewModel.Workspace.Selected is null) await viewModel.NewConversationAsync(CancellationToken.None); await viewModel.RefreshAsync(CancellationToken.None); Render(true); _refresh.Start(); };
        _refresh.Tick += (_,_) => { _=viewModel.Workspace.Selected?.RefreshUndoAsync(); Render(false); if(viewModel.HistoryNeedsRefresh && DateTime.UtcNow-_lastHistoryRefresh>TimeSpan.FromMilliseconds(500)) { _lastHistoryRefresh=DateTime.UtcNow; _=viewModel.RefreshAsync(CancellationToken.None); } };
        Closing += OnClosing;
        Closed += (_,_) => { _refresh.Stop(); _selection?.Cancel(); _pasteBehavior.Dispose(); viewModel.VisibilityChanged-=VisibilityChanged; };
    }
    public void AttachLifecycle(ExamHelper.App.Lifecycle.AppLifecycle lifecycle)
    {
        _lifecycle=lifecycle; lifecycle.ExitingChanged+=ExitingChanged; Closed+=(_,_) => lifecycle.ExitingChanged-=ExitingChanged;
    }
    private void ExitingChanged(bool exiting) { if(Dispatcher.CheckAccess()) MainRoot.IsEnabled=!exiting; else Dispatcher.BeginInvoke(() => MainRoot.IsEnabled=!exiting); }
    private void VisibilityChanged(bool visible) => Dispatcher.BeginInvoke(() => { if(visible) { Show(); if(WindowState==WindowState.Minimized) WindowState=WindowState.Normal; Activate(); Render(true); } else Hide(); });
    private async Task RunAsync(Func<Task> action) { try { await action(); } catch(OperationCanceledException) { } catch(Exception error) { _viewModel.Status=Common.ObservableObject.Explain(error); } }
    private async void StartClick(object sender,RoutedEventArgs e) => await RunAsync(async () => { await DraftController.FlushAsync(CancellationToken.None); await _viewModel.StartAsync(CancellationToken.None); });
    private async void NewClick(object sender,RoutedEventArgs e) => await RunAsync(async () => { await DraftController.FlushAsync(CancellationToken.None); await _viewModel.NewConversationAsync(CancellationToken.None); Render(true); HomeDraftInput.Focus(); });
    private async void SendClick(object sender,RoutedEventArgs e) => await RunAsync(() => DraftController.SendAsync(CancellationToken.None));
    private async void StopClick(object sender,RoutedEventArgs e) => await RunAsync(() => _viewModel.StopAsync(CancellationToken.None));
    private Task Command(AppCommand command) => RunAsync(() => DraftController.ExecuteAsync(command,CancellationToken.None));
    private async void RetryAnswerClick(object sender,RoutedEventArgs e) => await Command(AppCommand.RetryRequest);
    private async void DiscardAnswerClick(object sender,RoutedEventArgs e) => await Command(AppCommand.DiscardRequest);
    private async void UndoSpeechClick(object sender,RoutedEventArgs e) => await Command(AppCommand.UndoSpeech);
    private async void RetrySpeechClick(object sender,RoutedEventArgs e) => await Command(AppCommand.RetrySpeech);
    private async void DiscardSpeechClick(object sender,RoutedEventArgs e) => await Command(AppCommand.DiscardSpeech);
    private async void DraftTextChanged(object sender,TextChangedEventArgs e)
    { if(!_syncInput && DraftController is not null && _viewModel.Workspace.Selected is not null) await RunAsync(() => DraftController.UpdateTextAsync(HomeDraftInput.Text,CancellationToken.None)); }
    private async void SelectionChanged(object sender,SelectionChangedEventArgs e)
    {
        if(_syncSelection || _viewModel.IsBusy || HistoryList.SelectedItem is not HistoryItemViewModel item) return;
        await OpenHistoryAsync(item.Id);
    }
    private async void ExportClick(object sender,RoutedEventArgs e)
    {
        var snapshot=_viewModel.SnapshotForPresentation(); if(snapshot is null) return;
        var dialog=new Microsoft.Win32.SaveFileDialog { Title="导出此对话",Filter="HTML 文件|*.html",DefaultExt=".html",AddExtension=true,OverwritePrompt=true,FileName=ConversationExportController.DefaultFileName(snapshot) };
        if(dialog.ShowDialog(this)!=true) return;
        await RunAsync(async () => {
            var controller=new ConversationExportController(new(_viewModel.ImageStore,new ExamHelper.Core.Markdown.MarkdownHtmlRenderer()));
            await controller.WriteAsync(dialog.FileName,snapshot,CancellationToken.None); _viewModel.Status="对话已导出为离线 HTML。";
        });
    }
    private void UsageClick(object sender,RoutedEventArgs e) => new ExamHelper.App.Help.UsageWindow(_viewModel.SettingsSnapshot.Hotkeys.Bindings) { Owner=this }.Show();
    private async void SettingsClick(object sender,RoutedEventArgs e)
    {
        await RunAsync(async () =>
        {
            var settings=new SettingsWindow(await _settings()) { Owner=this }; _settingsWindow=settings;
            try { settings.ShowDialog(); } finally { _settingsWindow=null; }
            await _viewModel.RefreshAsync(CancellationToken.None); Render(true);
        });
    }
    private void DraftDragOver(object sender,DragEventArgs e)
    { if(!_viewModel.CanEditDraft) { e.Effects=DragDropEffects.None;e.Handled=true;return; } if(!e.Data.GetDataPresent(DataFormats.FileDrop)) return; e.Effects=DragDropEffects.Copy; e.Handled=true; }
    private async void DraftDrop(object sender,DragEventArgs e)
    { if(!_viewModel.CanEditDraft) { e.Handled=true;return; } if(e.Data.GetData(DataFormats.FileDrop) is string[] paths) { e.Handled=true; await RunAsync(() => DraftController.ImportFilesAsync(paths,CancellationToken.None)); } }
    private async void OnClosing(object? sender,CancelEventArgs e)
    {
        if(_closing || _lifecycle?.IsExitCommitted==true) return;
        if(_lifecycle is not null) { e.Cancel=true; _lifecycle.HideToTray(); return; }
        e.Cancel=true; if(_closePending) return; _closePending=true;
        try { await DraftController.FlushAsync(CancellationToken.None); await _shutdown(); _closing=true; Close(); if(Application.Current is ExamHelper.App.App) Application.Current.Shutdown(); }
        catch(Exception error) { _viewModel.Status=Common.ObservableObject.Explain(error); }
        finally { _closePending=false; }
    }
}
