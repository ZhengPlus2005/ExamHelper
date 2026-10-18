using ExamHelper.Core.Drafts;
using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Sessions;
public sealed partial class SessionCoordinator
{
    public async Task UpdateDraftTextAsync(string text,CancellationToken ct,string? expectedText = null)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed,this); var current=_draft.Snapshot().Text;
            if(expectedText is not null && current != expectedText && current.StartsWith(expectedText,StringComparison.Ordinal)) text += current[expectedText.Length..];
            if(current == text) return; _undo.Invalidate(); _draft.ReplaceText(text); Publish(); ScheduleDraftSave(); }
        finally { _gate.Release(); }
    }
    public async Task AppendImagesAsync(IReadOnlyList<ImageRef> images,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed,this); foreach(var image in images) if(!_draft.Snapshot().Images.Any(i => i.Id == image.Id)) _draft.AppendImage(image); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async Task SelectModelAsync(ModelSelection selection,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this); var settings = AiProfileCatalog.Normalize(_settings());
            ModelCatalog.Resolve(settings,selection,_state.IsAuxiliaryMode && _verification is not null,_verification ?? new(new byte[32]));
            var p = settings.AiProfiles!.Value.Single(p => p.Id == selection.ProfileId); var m = p.Models!.Value.Single(m => m.Id == selection.ModelId);
            _state = _state with { Selection = ModelCatalog.Select(p,m,selection.Thinking) }; Publish(); await SaveLockedAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task RenameAsync(string title,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(title)) throw new ArgumentException("标题不能为空。");
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed,this); _state = _state with { CustomTitle = title.Trim() }; Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async Task FlushAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed,this); Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    public async Task SetAuxiliaryModeAsync(bool auxiliary,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try { ObjectDisposedException.ThrowIf(_disposed,this); _state = _state with { IsAuxiliaryMode = auxiliary }; Publish(); await SaveLockedAsync(ct).ConfigureAwait(false); }
        finally { _gate.Release(); }
    }
    private AiSettings ResolveAi(AppSettings settings) => _state.Selection is { } choice
        ? ModelCatalog.Resolve(settings,choice,_state.IsAuxiliaryMode && _verification is not null,_verification ?? new(new byte[32])) : settings.AiProfiles is null ? settings.Ai : throw new InvalidOperationException("请先选择可用的配置和模型。");
    private static ModelSelection? InitialSelection(AppSettings settings)
    { try { return ModelCatalog.Default(settings); } catch(InvalidOperationException) { return null; } }
    private void GlobalGenerationChanged() { if(!_disposed) StateChanged?.Invoke(_state); }
}
