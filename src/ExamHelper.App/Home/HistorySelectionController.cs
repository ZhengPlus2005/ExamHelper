using System.Collections.Specialized;
using System.ComponentModel;
using ExamHelper.App.Common;
namespace ExamHelper.App.Home;
public sealed class HistorySelectionController : ObservableObject
{
    private readonly HistoryCollection _items;
    private readonly HashSet<HistoryItemViewModel> _subscribed=[];
    private bool _batch,_updating;
    public bool IsBatchMode { get => _batch; private set => Set(ref _batch,value); }
    public IReadOnlySet<Guid> SelectedIds=>_items.Where(i=>i.IsChecked).Select(i=>i.Id).ToHashSet();
    public int SelectedCount=>_items.Count(i=>i.IsChecked);
    public HistorySelectionController(HistoryCollection items)
    { _items=items; items.CollectionChanged+=CollectionChanged; SyncItems(); }
    private void SyncItems()
    {
        foreach(var item in _subscribed.Where(i=>!_items.Contains(i)).ToArray()) { item.PropertyChanged-=ItemChanged; _subscribed.Remove(item); }
        foreach(var item in _items) if(_subscribed.Add(item)) item.PropertyChanged+=ItemChanged;
    }
    private void CollectionChanged(object? sender,NotifyCollectionChangedEventArgs e) { if(e.Action!=NotifyCollectionChangedAction.Move) SyncItems(); Changed(); }
    private void ItemChanged(object? sender,PropertyChangedEventArgs e) { if(e.PropertyName==nameof(HistoryItemViewModel.IsChecked) && !_updating) Changed(); }
    private void Changed() { Notify(nameof(SelectedIds)); Notify(nameof(SelectedCount)); }
    public void Enter() { IsBatchMode=true; Changed(); }
    public void Complete() { SetAll(false); IsBatchMode=false; }
    public void ToggleRow(Guid id) { if(_items.FirstOrDefault(i=>i.Id==id) is { } item) item.IsChecked=!item.IsChecked; }
    public void SetChecked(Guid id,bool value) { if(_items.FirstOrDefault(i=>i.Id==id) is { } item) item.IsChecked=value; }
    public void SetAll(bool value)
    { _updating=true; try { foreach(var item in _items) item.IsChecked=value; } finally { _updating=false; Changed(); } }
}
