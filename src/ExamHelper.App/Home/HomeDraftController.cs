using System.Windows.Media.Imaging;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Images;
namespace ExamHelper.App.Home;
public sealed class HomeDraftController(Func<SessionCoordinator?> selected,IImageImporter importer)
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid,Task> _writes = [];
    private readonly HashSet<Task> _imports = [];
    private readonly Dictionary<Guid,string> _localTexts = [];
    public void ObserveText(Guid id,string text) { lock(_sync) _localTexts[id]=text; }
    public bool HasPendingEdits(Guid id) { lock(_sync) return _writes.TryGetValue(id,out var task) && !task.IsCompleted; }
    private SessionCoordinator Target() => selected() ?? throw new InvalidOperationException("请先新建或选择对话。");
    private Task Enqueue(SessionCoordinator target,Func<Task> action)
    {
        lock(_sync)
        {
            var previous = _writes.GetValueOrDefault(target.State.Id) ?? Task.CompletedTask;
            var next = AfterAsync(previous,action); _writes[target.State.Id] = next; return next;
        }
    }
    private static async Task AfterAsync(Task previous,Func<Task> action)
    { try { await previous.ConfigureAwait(false); } catch { } await action().ConfigureAwait(false); }
    public Task UpdateTextAsync(string text,CancellationToken ct)
    {
        var target=Target();
        lock(_sync) { var before=_localTexts.GetValueOrDefault(target.State.Id) ?? target.State.Draft.Text; _localTexts[target.State.Id]=text; return Enqueue(target,() => target.UpdateDraftTextAsync(text,ct,before)); }
    }
    public Task ExecuteAsync(AppCommand command,CancellationToken ct) { var target=Target(); return Enqueue(target,() => target.ExecuteAsync(command,ct)); }
    public Task SendAsync(CancellationToken ct)
    { var target = Target(); return Enqueue(target,() => target.ExecuteAsync(AppCommand.Send,ct)); }
    public async Task FlushAsync(CancellationToken ct)
    {
        var target = selected(); if(target is null) return;
        Task pending; lock(_sync) pending = _writes.GetValueOrDefault(target.State.Id) ?? Task.CompletedTask;
        try { await pending.WaitAsync(ct).ConfigureAwait(false); } catch when(!ct.IsCancellationRequested) { }
        await target.FlushAsync(ct).ConfigureAwait(false);
    }
    public Task ImportFilesAsync(IEnumerable<string> files,CancellationToken ct)
    { var target=Target(); return TrackImport(ImportFilesCoreAsync(target,files.ToArray(),ct)); }
    private async Task ImportFilesCoreAsync(SessionCoordinator target,string[] files,CancellationToken ct)
    { var imported=await importer.ImportFilesAsync(files,ct).ConfigureAwait(false); await target.AppendImagesAsync(imported,ct).ConfigureAwait(false); }
    public Task ImportBitmapAsync(BitmapSource bitmap,CancellationToken ct)
    { var target=Target(); return TrackImport(ImportBitmapCoreAsync(target,bitmap,ct)); }
    private async Task ImportBitmapCoreAsync(SessionCoordinator target,BitmapSource bitmap,CancellationToken ct)
    { var imported=await importer.ImportBitmapAsync(bitmap,ct).ConfigureAwait(false); await target.AppendImagesAsync(imported,ct).ConfigureAwait(false); }
    private Task TrackImport(Task task)
    {
        lock(_sync) _imports.Add(task);
        _=task.ContinueWith(completed => { lock(_sync) _imports.Remove(completed); },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        return task;
    }
    public async Task DrainAllAsync(CancellationToken ct)
    {
        while(true)
        {
            Task[] pending; lock(_sync) pending=_writes.Values.Concat(_imports).Where(t => !t.IsCompleted).ToArray();
            if(pending.Length==0) return;
            try { await Task.WhenAll(pending).WaitAsync(ct).ConfigureAwait(false); }
            catch when(!ct.IsCancellationRequested) { /* UI reports operation errors; final workspace flush retries persistence. */ }
        }
    }
}
