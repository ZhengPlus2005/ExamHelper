using System.Collections.ObjectModel;
using ExamHelper.Core.Sessions;
namespace ExamHelper.App.Home;
public sealed class HistoryCollection : ObservableCollection<HistoryItemViewModel>
{
    public void Apply(IReadOnlyList<SessionSummary> summaries)
    {
        var ordered=summaries.OrderByDescending(s=>s.SortAt).ThenByDescending(s=>s.CreatedAt).ThenBy(s=>s.Id.ToString("D"),StringComparer.Ordinal).ToArray();
        var desired=ordered.Select(s=>s.Id).ToHashSet();
        for(var i=Count-1;i>=0;i--) if(!desired.Contains(this[i].Id)) RemoveAt(i);
        var existing=this.ToDictionary(item=>item.Id);
        for(var i=0;i<ordered.Length;i++)
        {
            var summary=ordered[i];
            if(existing.TryGetValue(summary.Id,out var item)) { var from=IndexOf(item); if(from!=i) Move(from,i); }
            else { item=new(summary.Id); Insert(i,item); }
            item.Update(summary);
        }
    }
}
