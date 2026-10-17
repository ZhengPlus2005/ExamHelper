using System.Collections.Immutable;
namespace ExamHelper.Core.Settings;
public sealed record AiServiceProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "新配置";
    public AiSettings Settings { get; init; } = new();
    public string? VerifiedFingerprint { get; init; }
}
public static class AiProfileCatalog
{
    private static readonly Guid LegacyId = new("6c1ae390-a820-4f68-8ca3-119614ebc11b");
    public static AppSettings Normalize(AppSettings settings)
    {
        if(settings.AiProfiles is null)
        {
            var imported = new AiServiceProfile { Id = LegacyId,Name = settings.Ai.ProviderKind == ProviderKind.Kimi ? "KIMI 默认" : "原有配置",Settings = settings.Ai,VerifiedFingerprint = settings.Evidence.AiFingerprint };
            return settings with { AiProfiles = [imported],ActiveAiProfileId = imported.Id };
        }
        var active = settings.AiProfiles.Value.FirstOrDefault(p => p.Id == settings.ActiveAiProfileId);
        if(settings.ActiveAiProfileId is not null && active is null) throw new ArgumentException("启用的 AI 配置不存在。");
        return settings with { Ai = active?.Settings ?? new AiSettings(),Evidence = settings.Evidence with { AiFingerprint = active?.VerifiedFingerprint } };
    }
    public static AppSettings Activate(AppSettings settings,Guid id)
    {
        settings = Normalize(settings);
        if(!settings.AiProfiles!.Value.Any(p => p.Id == id)) throw new ArgumentException("AI 配置不存在。");
        return Normalize(settings with { ActiveAiProfileId = id });
    }
}
