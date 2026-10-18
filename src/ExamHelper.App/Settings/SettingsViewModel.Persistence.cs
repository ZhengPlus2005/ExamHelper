using System.Collections.Immutable;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Settings;

namespace ExamHelper.App.Settings;

public sealed partial class SettingsViewModel
{
    private TaskCompletionSource<bool>? _closeDecision;
    private bool _closingAccepted;
    private readonly HashSet<string> _pendingSecretCleanup = [];
    private AsrSettings BuildAsr() => _initial.Asr with
    {
        BaseUri = new(AsrBaseAddress.Trim()),Model = AsrModel.Trim(),DeviceId = DeviceId,
        SecretFingerprint = _verification.SecretDigest(AsrApiKey)
    };

    public async Task<bool> RequestCloseAsync(Func<Task<SettingsCloseChoice>> ask,CancellationToken ct)
    {
        var decision = _closeDecision = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var closes = false;
        try
        {
            await _saveGate.WaitAsync(ct); _saveGate.Release();
            if(!HasUnsavedChanges) return closes = true;
            var choice = await ask();
            if(choice == SettingsCloseChoice.Cancel) return false;
            if(choice == SettingsCloseChoice.Discard) return closes = true;
            try { await SaveAsync(ct); return closes = !HasUnsavedChanges; }
            catch(Exception error) { Status = Explain(error); return false; }
        }
        finally
        {
            _closingAccepted = closes;
            _closeDecision = null;
            decision.TrySetResult(!closes);
        }
    }

    public async Task SaveAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        await _saveGate.WaitAsync(linked.Token);
        try
        {
            var keys = AiProfiles.ToDictionary(p => p.BuildSettings().SecretId,p => p.ApiKey);
            var candidate = BuildCandidate(); keys.Add(candidate.Asr.SecretId,AsrApiKey);
            JsonSettingsStore.Validate(candidate);
            if(!candidate.Hotkeys.Bindings.SequenceEqual(_initial.Hotkeys.Bindings) &&
                ProbeHotkeys?.Invoke(candidate.Hotkeys.Bindings) is { Length: > 0 } conflict)
                throw new ArgumentException(conflict);
            await CommitAsync(candidate,keys,linked.Token);
            Status = HasUnsavedChanges ? "设置已保存，保存期间的新修改仍待保存。" : "设置已保存。";
            Notify();
        }
        finally { _saveGate.Release(); }
    }

    public async Task AutoSavePromptAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct,_lifetime.Token);
        await _saveGate.WaitAsync(linked.Token);
        try
        {
            if(_closingAccepted || _closeDecision is not null) return;
            var candidate = _initial with { DirectCapturePrompt = DirectCapturePrompt };
            await _store.SaveAsync(candidate,linked.Token); _initial = candidate; _saved(candidate);
        }
        finally { _saveGate.Release(); }
    }

    // Tests commit only the immutable configuration actually used by their request.
    // Other form edits remain drafts, including changes made while I/O is pending.
    private async Task<bool> PersistAiTestAsync(AiProfileEditor editor,AiServiceProfile snapshot,Guid modelId,string key,string fingerprint,CancellationToken ct)
    {
        if(!await MayCommitTestAsync(ct)) return false;
        await _saveGate.WaitAsync(ct);
        try
        {
            if(_closingAccepted || _closeDecision is not null || !AiProfiles.Contains(editor)) return false;
            var model = editor.Models.FirstOrDefault(m => m.Id == modelId);
            if(model is null || _verification.AiFingerprint(new() { Ai = editor.BuildSettings() with { Model = model.Name.Trim() } }) != fingerprint)
            { Status = "配置已变化，本次结果不计入验证资格，请重新测试。"; return false; }
            var profiles = _initial.AiProfiles!.Value;
            var index = Array.FindIndex(profiles.ToArray(),p => p.Id == snapshot.Id);
            var testedModel = snapshot.Models!.Value.Single(m => m.Id == modelId) with { VerifiedFingerprint = fingerprint };
            var savedModels = index < 0 ? ImmutableArray<AiModelProfile>.Empty : profiles[index].Models!.Value;
            var modelIndex = Array.FindIndex(savedModels.ToArray(),m => m.Id == modelId);
            savedModels = modelIndex < 0 ? savedModels.Add(testedModel) : savedModels.SetItem(modelIndex,testedModel);
            var tested = snapshot with { Models = savedModels,DefaultModelId = index < 0 ? modelId : profiles[index].DefaultModelId,VerifiedFingerprint = fingerprint };
            profiles = index < 0 ? profiles.Add(tested) : profiles.SetItem(index,tested);
            var candidate = AiProfileCatalog.Normalize(_initial with { AiProfiles = profiles });
            await CommitAsync(candidate,new Dictionary<string,string> { [snapshot.Settings.SecretId] = key },ct);
            var committed = _initial.AiProfiles!.Value.Single(p => p.Id == snapshot.Id);
            model.VerifiedFingerprint = committed.Models!.Value.Single(m => m.Id == modelId).VerifiedFingerprint;
            return true;
        }
        finally { _saveGate.Release(); }
    }

    private async Task<bool> PersistSpeechTestAsync(CancellationToken ct)
    {
        if(!await MayCommitTestAsync(ct)) return false;
        await _saveGate.WaitAsync(ct);
        try
        {
            if(_closingAccepted || _closeDecision is not null) return false;
            _verification.RecordAsrSuccess(_initial with { Asr = BuildAsr() },_speechResult!.Fingerprint);
            var candidate = _verification.RecordAsrSuccess(_initial with { Asr = _speechSettings!.Asr },_speechResult.Fingerprint);
            await CommitAsync(candidate,new Dictionary<string,string> { [candidate.Asr.SecretId] = _speechKey },ct);
            return true;
        }
        finally { _saveGate.Release(); }
    }

    private async Task<bool> MayCommitTestAsync(CancellationToken ct)
    {
        while(_closeDecision is { } decision)
            if(!await decision.Task.WaitAsync(ct)) return false;
        return !_closingAccepted && !_disposed;
    }

    // Credentials use new identities before the JSON pointer is committed. A failed
    // JSON save can therefore never replace a credential referenced by the last save.
    private async Task CommitAsync(AppSettings candidate,IReadOnlyDictionary<string,string> keys,CancellationToken ct)
    {
        var staged = new HashSet<string>();
        var oldReferences = SecretIds(_initial);
        try
        {
            var profiles = candidate.AiProfiles!.Value.ToBuilder();
            for(var i = 0;i < profiles.Count;i++)
            {
                var profile = profiles[i];
                if(!keys.TryGetValue(profile.Settings.SecretId,out var key)) continue;
                var id = await StageSecretAsync(profile.Settings.SecretId,key,"ai",staged,ct);
                var original = profile.Settings;
                var models = profile.Models!.Value.Select(m =>
                {
                    var old = new AppSettings { Ai = original with { Model = m.Name },Evidence = new(m.VerifiedFingerprint) };
                    return _verification.IsAiVerified(old) ? m with { VerifiedFingerprint = _verification.AiFingerprint(old with { Ai = old.Ai with { SecretId = id } }) } : m;
                }).ToImmutableArray();
                profile = profile with { Settings = profile.Settings with { SecretId = id },Models = models };
                profiles[i] = profile;
            }
            var asrQualified = _verification.IsAsrVerified(candidate);
            if(keys.TryGetValue(candidate.Asr.SecretId,out var asrKey))
            {
                var id = await StageSecretAsync(candidate.Asr.SecretId,asrKey,"asr",staged,ct);
                candidate = candidate with { Asr = candidate.Asr with { SecretId = id } };
                if(asrQualified)
                    candidate = candidate with { Evidence = candidate.Evidence with { AsrFingerprint = _verification.AsrFingerprint(candidate) } };
            }
            candidate = AiProfileCatalog.Normalize(candidate with { AiProfiles = profiles.ToImmutable() });
            JsonSettingsStore.Validate(candidate);
            await _store.SaveAsync(candidate,ct);
        }
        catch
        {
            _pendingSecretCleanup.UnionWith(staged);
            await CleanupSecretsAsync(CancellationToken.None);
            throw;
        }
        _initial = candidate; _evidence = candidate.Evidence;
        foreach(var profile in candidate.AiProfiles!.Value)
            AiProfiles.FirstOrDefault(p => p.Id == profile.Id)?.RebaseSecretId(profile.Settings.SecretId);
        _pendingSecretCleanup.UnionWith(oldReferences.Except(SecretIds(candidate)));
        _saved(candidate);
        await CleanupSecretsAsync(CancellationToken.None);
    }

    private async Task<string> StageSecretAsync(string oldId,string key,string prefix,HashSet<string> staged,CancellationToken ct)
    {
        var previous = await _secrets.GetAsync(oldId,ct);
        if((previous ?? "") == key) return oldId;
        var id = prefix + "-" + Guid.NewGuid().ToString("N");
        staged.Add(id);
        await _secrets.SetAsync(id,key,ct);
        return id;
    }

    private async Task CleanupSecretsAsync(CancellationToken ct)
    {
        var live = SecretIds(_initial);
        foreach(var id in _pendingSecretCleanup.ToArray())
        {
            if(live.Contains(id)) { _pendingSecretCleanup.Remove(id); continue; }
            try { await _secrets.SetAsync(id,"",ct); _pendingSecretCleanup.Remove(id); }
            catch { /* Retain for the next successful save; committed settings stay usable. */ }
        }
    }

    private static HashSet<string> SecretIds(AppSettings settings) =>
        (settings.AiProfiles ?? []).Select(p => p.Settings.SecretId).Append(settings.Asr.SecretId).ToHashSet();
}

