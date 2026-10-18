using System.Windows.Media.Imaging;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Windows.Images;
namespace ExamHelper.App.Home;
public sealed class HomeDraftController(Func<SessionCoordinator?> selected,IImageImporter importer)
{
    private readonly object _sync = new();
    private readonly Dictionary<Guid,Task> _writes = [];
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
        await pending.WaitAsync(ct).ConfigureAwait(false); await target.FlushAsync(ct).ConfigureAwait(false);
    }
    public async Task ImportFilesAsync(IEnumerable<string> files,CancellationToken ct)
    { var target = Target(); var imported = await importer.ImportFilesAsync(files,ct).ConfigureAwait(false); await target.AppendImagesAsync(imported,ct).ConfigureAwait(false); }
    public async Task ImportBitmapAsync(BitmapSource bitmap,CancellationToken ct)
    { var target = Target(); var imported = await importer.ImportBitmapAsync(bitmap,ct).ConfigureAwait(false); await target.AppendImagesAsync(imported,ct).ConfigureAwait(false); }
}
