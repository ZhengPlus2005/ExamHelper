using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
namespace ExamHelper.App.Home;
public partial class HomeWindow
{
    private void CancelHistoryLoad() { _selection?.Cancel(); _selection?.Dispose(); _selection=null; }
    private async Task OpenHistoryAsync(Guid id)
    {
        if(id==_viewModel.SelectedConversationId) return;
        CancelHistoryLoad(); _selection=new(); var token=_selection.Token;
        await RunAsync(async () => { await DraftController.FlushAsync(token); await _viewModel.SelectAsync(id,token); if(!token.IsCancellationRequested) Render(true); });
    }
    private async void HistoryRowClick(object sender,MouseButtonEventArgs e)
    {
        if(!_viewModel.HistorySelection.IsBatchMode || _viewModel.IsBusy) return;
        var origin=e.OriginalSource as DependencyObject;
        for(var node=origin;node is not null && node!=HistoryList;node=node is FrameworkContentElement content ? content.Parent : VisualTreeHelper.GetParent(node))
            if(node is ButtonBase) return;
        while(origin is FrameworkContentElement text) origin=text.Parent;
        if(origin is null || ItemsControl.ContainerFromElement(HistoryList,origin) is not ListBoxItem { DataContext:HistoryItemViewModel item }) return;
        e.Handled=true; _viewModel.HistorySelection.ToggleRow(item.Id);
        _syncSelection=true; try { HistoryList.SelectedItem=item; } finally { _syncSelection=false; }
        await OpenHistoryAsync(item.Id);
    }
    private void HistoryCheckClick(object sender,RoutedEventArgs e) => e.Handled=true;
    private void BeginBatchClick(object sender,RoutedEventArgs e) => _viewModel.HistorySelection.Enter();
    private void FinishBatchClick(object sender,RoutedEventArgs e) => _viewModel.HistorySelection.Complete();
    private void SelectAllHistoryClick(object sender,RoutedEventArgs e) => _viewModel.HistorySelection.SetAll(true);
    private void ClearHistorySelectionClick(object sender,RoutedEventArgs e) => _viewModel.HistorySelection.SetAll(false);
    private async void DeleteSelectedClick(object sender,RoutedEventArgs e)
    {
        CancelHistoryLoad(); var ids=_viewModel.HistorySelection.SelectedIds.ToArray();
        await RunAsync(async () => { await _viewModel.DeleteManyAsync(ids,count=>Task.FromResult(MessageBox.Show(this,$"删除所选 {count} 条对话及其不再被引用的图片？","批量删除",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes),CancellationToken.None); Render(true); });
    }
    private async void DeleteHistoryRowClick(object sender,RoutedEventArgs e)
    {
        e.Handled=true; if(_viewModel.IsBusy || sender is not FrameworkElement { DataContext:HistoryItemViewModel item }) return;
        CancelHistoryLoad();
        await RunAsync(async () => { await _viewModel.DeleteManyAsync([item.Id],_=>Task.FromResult(MessageBox.Show(this,$"删除对话“{item.Title}”及其不再被引用的图片？","删除历史",MessageBoxButton.YesNo,MessageBoxImage.Question)==MessageBoxResult.Yes),CancellationToken.None); Render(true); });
    }
    private async void RenameHistoryRowClick(object sender,RoutedEventArgs e)
    {
        e.Handled=true; if(_viewModel.IsBusy || sender is not FrameworkElement { DataContext:HistoryItemViewModel item }) return;
        var id=item.Id; var input=new TextBox { Text=item.Title,Margin=new Thickness(14),MinWidth=330,Padding=new Thickness(8) };
        var save=new Button { Content="保存标题",IsDefault=true,Margin=new Thickness(14,0,14,14),Padding=new Thickness(12,7,12,7) };
        var panel=new StackPanel(); panel.Children.Add(input); panel.Children.Add(save);
        var dialog=new Window { Icon=Icon,Title="重命名",Content=panel,SizeToContent=SizeToContent.WidthAndHeight,ResizeMode=ResizeMode.NoResize,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        save.Click+=(_,_) => { if(!string.IsNullOrWhiteSpace(input.Text)) dialog.DialogResult=true; }; dialog.Loaded+=(_,_) => { input.Focus(); input.SelectAll(); };
        if(dialog.ShowDialog()==true) await RunAsync(()=>_viewModel.RenameAsync(id,input.Text,CancellationToken.None));
    }
    private async void RetryImageCleanupClick(object sender,RoutedEventArgs e) => await RunAsync(()=>_viewModel.RetryImageCleanupAsync(CancellationToken.None));
}
