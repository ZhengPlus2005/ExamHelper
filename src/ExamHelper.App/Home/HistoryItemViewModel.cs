using ExamHelper.App.Common;
using ExamHelper.Core.Common;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Home;
public sealed class HistoryItemViewModel(Guid id) : ObservableObject
{
    private string _title="",_timeText="";
    private DateTimeOffset _sortAt;
    private bool _isChecked;
    public bool IsChecked { get => _isChecked; set => Set(ref _isChecked,value); }
    public Guid Id { get; } = id;
    public string Title { get => _title; private set => Set(ref _title,value); }
    public DateTimeOffset SortAt { get => _sortAt; private set => Set(ref _sortAt,value); }
    public string TimeText { get => _timeText; private set => Set(ref _timeText,value); }
    public void Update(SessionSummary summary)
    {
        if(summary.Id != Id) throw new ArgumentException("历史条目身份不匹配。");
        Title=summary.Title; SortAt=summary.SortAt; TimeText=DisplayTime.Format(summary.SortAt);
    }
}
