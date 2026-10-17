using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace ExamHelper.App.Settings;
public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly DispatcherTimer _meter = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly DispatcherTimer _promptSave = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private bool _loaded;
    public SettingsWindow(SettingsViewModel viewModel)
    {
        _viewModel = viewModel; InitializeComponent(); DataContext = viewModel;
        AiKey.Password = viewModel.AiApiKey; AsrKey.Password = viewModel.AsrApiKey;
        _meter.Tick += (_,_) => viewModel.RefreshTelemetry(); _meter.Start();
        _promptSave.Tick += async (_,_) => { _promptSave.Stop(); try { await viewModel.AutoSavePromptAsync(CancellationToken.None); } catch(Exception error) { viewModel.Status = Common.ObservableObject.Explain(error); } };
        Loaded += (_,_) => _loaded = true;
        Closed += async (_,_) => { _loaded = false; _meter.Stop(); _promptSave.Stop(); try { await viewModel.DisposeAsync(); } catch { } };
    }
    private void AiPasswordChanged(object sender,RoutedEventArgs args) { if(_viewModel is not null) _viewModel.AiApiKey = ((PasswordBox)sender).Password; }
    private void AsrPasswordChanged(object sender,RoutedEventArgs args) { if(_viewModel is not null) _viewModel.AsrApiKey = ((PasswordBox)sender).Password; }
    private void PromptChanged(object sender,TextChangedEventArgs args) { if(_loaded) { _promptSave.Stop(); _promptSave.Start(); } }
    private async void TextTestClick(object sender,RoutedEventArgs args) => await _viewModel.RunTextTestAsync(CancellationToken.None);
    private async void ImageTestClick(object sender,RoutedEventArgs args) => await _viewModel.RunImageTestAsync(CancellationToken.None);
    private async void SpeechTestClick(object sender,RoutedEventArgs args) { try { await _viewModel.ToggleSpeechTestAsync(CancellationToken.None); } catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); } }
    private async void ConfirmSpeechClick(object sender,RoutedEventArgs args) => await _viewModel.ConfirmSpeechAsync(CancellationToken.None);
    private void CloseClick(object sender,RoutedEventArgs args) => Close();
    private async void SaveClick(object sender,RoutedEventArgs args)
    {
        try { await _viewModel.SaveAsync(CancellationToken.None); Close(); }
        catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); }
    }
}