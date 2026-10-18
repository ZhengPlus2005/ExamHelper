using System.Collections.Immutable;
namespace ExamHelper.Core.Settings;
public sealed record AiServiceProfile
{
    private string? _baseAddress;
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public AiSettings Settings { get; init; } = new() { SecretId = "ai-" + Guid.NewGuid().ToString("N") };
    public string BaseAddress { get => _baseAddress ?? Settings.BaseUri?.ToString() ?? ""; init => _baseAddress = value; }
    public string? VerifiedFingerprint { get; init; }
    public ImmutableArray<AiModelProfile>? Models { get; init; } = [];
    public Guid? DefaultModelId { get; init; }
}
public static class AiProfileCatalog
{
    public static AppSettings Normalize(AppSettings settings)
    {
        if(settings.Ai is null || settings.Asr is null || settings.Ui is null || settings.Hotkeys is null || settings.Evidence is null)
            throw new ArgumentException("设置结构不完整。");
        var source = settings.AiProfiles ?? [];
        if(source.IsDefault || source.Any(p => p is null || p.Settings is null || p.Models is { IsDefault: true } || p.Models is { } models && models.Any(m => m is null || m.Name is null || m.DefaultThinking is null)))
            throw new ArgumentException("AI 配置结构无效。");
        var profiles = source.Select(p =>
        {
            var models = p.Models ?? [];
            var selected = models.FirstOrDefault(m => m.Id == p.DefaultModelId);
            Uri.TryCreate(p.BaseAddress.Trim(),UriKind.Absolute,out var endpoint);
            return p with { Models = models,BaseAddress = p.BaseAddress,Settings = p.Settings with
            {
                BaseUri = endpoint,Model = selected?.Name ?? "",Thinking = selected?.DefaultThinking ?? new(),
                ThinkingCapability = selected is null ? new() : ThinkingCapability.For(selected)
            },VerifiedFingerprint = selected?.VerifiedFingerprint };
        }).ToImmutableArray();
        if(source.SequenceEqual(profiles)) profiles = source;
        var active = profiles.FirstOrDefault(p => p.Id == settings.ActiveAiProfileId);
        if(settings.ActiveAiProfileId is not null && active is null) throw new ArgumentException("启用的 AI 配置不存在。");
        return settings with { AiProfiles = profiles,Ai = active?.Settings ?? new AiSettings(),
            Evidence = settings.Evidence with { AiFingerprint = active?.VerifiedFingerprint } };
    }
    public static AppSettings Activate(AppSettings settings,Guid id)
    {
        settings = Normalize(settings);
        if(!settings.AiProfiles!.Value.Any(p => p.Id == id)) throw new ArgumentException("AI 配置不存在。");
        return Normalize(settings with { ActiveAiProfileId = id });
    }
}
