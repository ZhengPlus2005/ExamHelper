using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using ExamHelper.Core.Interaction;
namespace ExamHelper.Core.Settings;
public sealed record AiServiceProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "新配置";
    public AiSettings Settings { get; init; } = new();
    public string? VerifiedFingerprint { get; init; }
    public ImmutableArray<AiModelProfile>? Models { get; init; }
    public Guid? DefaultModelId { get; init; }
}
public static class AiProfileCatalog
{
    private static readonly Guid LegacyId = new("6c1ae390-a820-4f68-8ca3-119614ebc11b");
    public static AppSettings Normalize(AppSettings settings)
    {
        var legacy = settings.FormatVersion < 3;
        var bindings = settings.Hotkeys.Bindings;
        if(!bindings.IsDefault)
            if(bindings.Any(b => b.Command == AppCommand.HideOverlay || (settings.FormatVersion < 2 && b.Command == AppCommand.Exit)))
                bindings = bindings.Where(b => b.Command != AppCommand.HideOverlay && !(settings.FormatVersion < 2 && b.Command == AppCommand.Exit)).ToImmutableArray();
        var profiles = settings.AiProfiles ?? [new AiServiceProfile { Id = LegacyId,Name = settings.Ai.ProviderKind == ProviderKind.Kimi ? "KIMI 默认" : "原有配置",Settings = settings.Ai,VerifiedFingerprint = settings.Evidence.AiFingerprint }];
        var activeId = settings.AiProfiles is null ? LegacyId : settings.ActiveAiProfileId;
        var normalized = profiles.Select(p => ImportModels(p,legacy)).ToImmutableArray();
        if(!profiles.SequenceEqual(normalized)) profiles = normalized;
        var active = profiles.FirstOrDefault(p => p.Id == activeId);
        if(activeId is not null && active is null) throw new ArgumentException("启用的 AI 配置不存在。");
        return settings with { FormatVersion = Math.Max(3,settings.FormatVersion),Hotkeys = settings.Hotkeys with { Bindings = bindings },AiProfiles = profiles,ActiveAiProfileId = activeId,
            Ai = active?.Settings ?? new AiSettings(),Evidence = settings.Evidence with { AiFingerprint = active?.VerifiedFingerprint } };
    }
    private static AiServiceProfile ImportModels(AiServiceProfile p,bool legacy)
    {
        if(p.Models is null)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(p.Id + "/" + p.Settings.Model));
            var model = new AiModelProfile { Id = new Guid(bytes.AsSpan(0,16)),Name = p.Settings.Model,VerifiedFingerprint = p.VerifiedFingerprint,
                DefaultThinking = legacy && p.Settings.Model == "kimi-k2.6" ? new(ThinkingKind.Disabled) : new() };
            return ImportModels(p with { Models = [model],DefaultModelId = model.Id },false);
        }
        // Only the explicit default is mirrored for legacy callers; never choose a replacement.
        var selected = p.Models.Value.FirstOrDefault(m => m.Id == p.DefaultModelId);
        return selected is null ? p : p with { Settings = p.Settings with { Model = selected.Name,Thinking = selected.DefaultThinking,ThinkingCapability = ThinkingCapability.For(selected) },VerifiedFingerprint = selected.VerifiedFingerprint };
    }
    public static AppSettings Activate(AppSettings settings,Guid id)
    {
        settings = Normalize(settings);
        if(!settings.AiProfiles!.Value.Any(p => p.Id == id)) throw new ArgumentException("AI 配置不存在。");
        return Normalize(settings with { ActiveAiProfileId = id });
    }
}
