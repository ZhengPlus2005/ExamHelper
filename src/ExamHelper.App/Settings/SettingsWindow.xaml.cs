using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace ExamHelper.App.Settings;
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly Func<Task<SettingsCloseChoice>> _confirmClose;
    private readonly DispatcherTimer _meter = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _promptSave = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private TaskCompletionSource<bool>? _closeResult;
    public Task<bool> TryCloseAsync()
    {
        if(_allowClose) return Task.FromResult(true);
        if(_closeResult is { Task.IsCompleted: false }) return _closeResult.Task;
        _closeResult=new(TaskCreationOptions.RunContinuationsAsynchronously); if(!_closing) Close(); return _closeResult.Task;
    }
    private bool _loaded,_allowClose,_closing,_syncingAiKey;
    public SettingsWindow(SettingsViewModel viewModel,Func<Task<SettingsCloseChoice>>? confirmClose = null)
    {
        _viewModel = viewModel; _confirmClose = confirmClose ?? AskSaveAsync;
        InitializeComponent(); DataContext = viewModel;
        SyncAiKey(); AsrKey.Password = viewModel.AsrApiKey;
        viewModel.PropertyChanged += ViewModelChanged;
        _meter.Tick += (_,_) => viewModel.RefreshTelemetry(); _meter.Start();
        _promptSave.Tick += async (_,_) => { _promptSave.Stop(); try { await viewModel.AutoSavePromptAsync(CancellationToken.None); } catch(Exception error) { viewModel.Status = Common.ObservableObject.Explain(error); } };
        Loaded += (_,_) => _loaded = true;
        Closing += OnClosing;
        Closed += async (_,_) =>
        {
            _loaded = false; _meter.Stop(); _promptSave.Stop(); viewModel.PropertyChanged -= ViewModelChanged;
            try { await viewModel.DisposeAsync(); } catch { }
        };
    }
    private void ViewModelChanged(object? sender,PropertyChangedEventArgs args)
    {
        if(args.PropertyName is nameof(SettingsViewModel.SelectedAiProfile) or nameof(SettingsViewModel.AiApiKey)) SyncAiKey();
    }
    private void SyncAiKey()
    {
        if(AiKey is null || AiKey.Password == _viewModel.AiApiKey) return;
        _syncingAiKey = true; try { AiKey.Password = _viewModel.AiApiKey; } finally { _syncingAiKey = false; }
    }
    private Task<SettingsCloseChoice> AskSaveAsync()
    {
        var result = MessageBox.Show(this,"设置中有尚未保存的修改，是否保存？\n选择“否”放弃这些修改，选择“取消”继续编辑。","保存设置",MessageBoxButton.YesNoCancel,MessageBoxImage.Question);
        return Task.FromResult(result == MessageBoxResult.Yes ? SettingsCloseChoice.Save : result == MessageBoxResult.No ? SettingsCloseChoice.Discard : SettingsCloseChoice.Cancel);
    }
    private async void OnClosing(object? sender,CancelEventArgs args)
    {
        if(_allowClose) return;
        args.Cancel = true; if(_closing) return; _closing = true;
        var pendingPrompt = _promptSave.IsEnabled; _promptSave.Stop();
        try
        {
            if(await _viewModel.RequestCloseAsync(_confirmClose,CancellationToken.None))
            { _allowClose = true; _closeResult?.TrySetResult(true); _ = Dispatcher.BeginInvoke(Close); }
            else { _closeResult?.TrySetResult(false); if(pendingPrompt) _promptSave.Start(); }
        }
        catch(Exception error) { _closeResult?.TrySetResult(false); _viewModel.Status = Common.ObservableObject.Explain(error); }
        finally { _closing = false; }
    }
    private async void FetchModelsClick(object sender,RoutedEventArgs args) => await _viewModel.FetchModelsAsync(CancellationToken.None);
    private void AddDiscoveredModelsClick(object sender,RoutedEventArgs args) => _viewModel.AddDiscoveredModels();
    private void AddModelClick(object sender,RoutedEventArgs args) => _viewModel.SelectedAiProfile?.AddModel();
    private void DeleteModelClick(object sender,RoutedEventArgs args) => _viewModel.SelectedAiProfile?.DeleteSelectedModel();
    private void DefaultModelClick(object sender,RoutedEventArgs args) { if(_viewModel.SelectedAiProfile is { SelectedModel: { } model } p) p.DefaultModelId = model.Id; }
    private void AddProfileClick(object sender,RoutedEventArgs args) => _viewModel.AddAiProfile();
    private void ActivateProfileClick(object sender,RoutedEventArgs args) => _viewModel.ActivateSelectedAiProfile();
    private void DeleteProfileClick(object sender,RoutedEventArgs args)
    {
        if(_viewModel.SelectedAiProfile is not { } profile) return;
        if(MessageBox.Show(this,$"删除 AI 配置“{profile.Name}”？\n保存后生效；不会删除历史对话。","删除配置",MessageBoxButton.YesNo,MessageBoxImage.Question) == MessageBoxResult.Yes)
            _viewModel.DeleteSelectedAiProfile();
    }
    private void AiPasswordChanged(object sender,RoutedEventArgs args) { if(!_syncingAiKey && _viewModel is not null) _viewModel.AiApiKey = ((PasswordBox)sender).Password; }
    private void AsrPasswordChanged(object sender,RoutedEventArgs args) { if(_viewModel is not null) _viewModel.AsrApiKey = ((PasswordBox)sender).Password; }
    private void PromptChanged(object sender,TextChangedEventArgs args) { if(_loaded) { _promptSave.Stop(); _promptSave.Start(); } }
    private async void TextTestClick(object sender,RoutedEventArgs args) => await _viewModel.RunTextTestAsync(CancellationToken.None);
    private async void ImageTestClick(object sender,RoutedEventArgs args) => await _viewModel.RunImageTestAsync(CancellationToken.None);
    private async void SpeechTestClick(object sender,RoutedEventArgs args) { try { await _viewModel.ToggleSpeechTestAsync(CancellationToken.None); } catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); } }
    private async void ConfirmSpeechClick(object sender,RoutedEventArgs args) => await _viewModel.ConfirmSpeechAsync(CancellationToken.None);
    private void CloseClick(object sender,RoutedEventArgs args) => Close();
    private async void SaveClick(object sender,RoutedEventArgs args)
    {
        try { _promptSave.Stop(); await _viewModel.SaveAsync(CancellationToken.None); Close(); }
        catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); }
    }
}

