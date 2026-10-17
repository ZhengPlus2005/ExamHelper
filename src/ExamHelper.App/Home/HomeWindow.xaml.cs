using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExamHelper.App.Composition;
using ExamHelper.App.Settings;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Home;
public partial class HomeWindow : Window
{
    private readonly HomeViewModel _viewModel;
    private readonly Func<Task<SettingsViewModel>> _settings;
    private readonly Func<Task> _shutdown;
    private CancellationTokenSource? _selection;
    private bool _closing;
    public HomeWindow(AppComposition services) : this(services.Home,services.CreateSettingsViewModelAsync,
        async () => { await services.Coordinator.ExitAsync(CancellationToken.None); await services.DisposeAsync(); }) { }
    public HomeWindow(HomeViewModel viewModel,Func<Task<SettingsViewModel>> settings,Func<Task> shutdown)
    {
        _viewModel = viewModel; _settings = settings; _shutdown = shutdown;
        InitializeComponent(); DataContext = viewModel;
        viewModel.VisibilityChanged += visible => Dispatcher.BeginInvoke(() => { if(visible) { Show(); Activate(); } else Hide(); });
        Loaded += async (_,_) => await viewModel.RefreshAsync(CancellationToken.None);
        Closing += OnClosing;
    }
    private async void StartClick(object sender,RoutedEventArgs args) => await _viewModel.StartAsync(CancellationToken.None);
    private async void ContinueClick(object sender,RoutedEventArgs args)
    { if(HistoryList.SelectedItem is SessionSummary session) await _viewModel.ContinueAsync(session.Id,CancellationToken.None); }
    private async void DeleteClick(object sender,RoutedEventArgs args)
    {
        if(HistoryList.SelectedItem is not SessionSummary session) return;
        await _viewModel.DeleteAsync(session.Id,() => Task.FromResult(MessageBox.Show(this,"删除此对话及其不再被其他对话引用的图片？","删除历史",MessageBoxButton.YesNo,MessageBoxImage.Question) == MessageBoxResult.Yes),CancellationToken.None);
        HistoryPictures.Children.Clear();
    }
    private async void SettingsClick(object sender,RoutedEventArgs args)
    {
        try
        {
            var settings = new SettingsWindow(await _settings()) { Owner = this };
            settings.ShowDialog(); await _viewModel.RefreshAsync(CancellationToken.None);
        }
        catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); }
    }
    private async void SelectionChanged(object sender,SelectionChangedEventArgs args)
    {
        _selection?.Cancel(); _selection?.Dispose(); _selection = new();
        if(HistoryList.SelectedItem is not SessionSummary session) return;
        var token = _selection.Token; await _viewModel.SelectAsync(session.Id,token);
        if(token.IsCancellationRequested) return;
        HistoryPictures.Children.Clear();
        foreach(var image in _viewModel.HistoryImages)
        {
            try
            {
                using var stream = new MemoryStream(image.Bytes); var bitmap = new BitmapImage();
                bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze();
                HistoryPictures.Children.Add(new Image { Source = bitmap,Margin = new Thickness(0,12,0,0),MaxHeight = 440,Stretch = System.Windows.Media.Stretch.Uniform });
            }
            catch { _viewModel.Status = "部分历史图片格式无法预览。"; }
        }
    }
    private async void OnClosing(object? sender,CancelEventArgs args)
    {
        if(_closing) return;
        args.Cancel = true;
        try
        {
            await _shutdown();
            _closing = true; _selection?.Cancel(); Close(); if(Application.Current is ExamHelper.App.App) Application.Current.Shutdown();
        }
        catch(Exception error) { _viewModel.Status = Common.ObservableObject.Explain(error); }
    }
}