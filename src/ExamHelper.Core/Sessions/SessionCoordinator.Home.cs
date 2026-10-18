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
    public Task AppendImagesAsync(IReadOnlyList<ImageRef> images,CancellationToken ct) => AppendImagesCoreAsync(images,null,ct);
    public Task AppendImportedImagesAsync(IReadOnlyList<ImageRef> images,Action accepted,CancellationToken ct) => AppendImagesCoreAsync(images,accepted,ct);
    private async Task AppendImagesCoreAsync(IReadOnlyList<ImageRef> images,Action? accepted,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this);
            foreach(var image in images) if(!_draft.Snapshot().Images.Any(i=>i.Id==image.Id)) _draft.AppendImage(image);
            // Ownership transfers before persistence, so a failed save must retain live draft images.
            accepted?.Invoke();Publish();await SaveLockedAsync(ct).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }
    public async Task SelectModelAsync(ModelSelection selection,CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed,this); var settings = AiProfileCatalog.Normalize(_settings());
            if(_state.IsAuxiliaryMode && _verification is null) throw new InvalidOperationException("服务验证不可用，请返回主页重试。");
            var p=settings.AiProfiles!.Value.FirstOrDefault(p=>p.Id==selection.ProfileId) ?? throw new InvalidOperationException("配置不可用，请重新选择。");
            ModelSelection next;
            if(selection.ModelId==Guid.Empty) next=ModelCatalog.Pending(p);
            else
            {
                ModelCatalog.Resolve(settings,selection,_state.IsAuxiliaryMode,_verification ?? new(new byte[32]));
                var m=p.Models!.Value.Single(m=>m.Id==selection.ModelId);next=ModelCatalog.Select(p,m,selection.Thinking);
            }
            _state = _state with { Selection = next }; Publish(); await SaveLockedAsync(ct).ConfigureAwait(false);
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
    private AiSettings ResolveAi(AppSettings settings)
    {
        if(_state.IsAuxiliaryMode && _verification is null) throw new InvalidOperationException("服务验证不可用，请返回主页重试。");
        if(_state.Selection is not { } choice) throw new InvalidOperationException("请先选择可用的配置和模型。");
        return ModelCatalog.Resolve(settings,choice,_state.IsAuxiliaryMode,_verification ?? new(new byte[32]));
    }
    private static ModelSelection? InitialSelection(AppSettings settings)
    { try { return ModelCatalog.Default(settings); } catch(InvalidOperationException) { return null; } }
    private void GlobalGenerationChanged() { if(!_disposed) StateChanged?.Invoke(_state); }
}
