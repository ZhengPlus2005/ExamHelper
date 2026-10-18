using System.Collections.Immutable;
namespace ExamHelper.Core.Settings;
public sealed record AiModelProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public ThinkingChoice DefaultThinking { get; init; } = new();
    public ThinkingCapability? CapabilityOverride { get; init; }
    public ModelMetadata? DiscoveryMetadata { get; init; }
    public string? TextVerifiedFingerprint { get; init; }
    public string? VerifiedFingerprint { get; init; }
}
public sealed record ModelSelection(Guid ProfileId,Guid ModelId,ThinkingChoice Thinking)
{
    public string ProfileName { get; init; } = "";
    public string ModelName { get; init; } = "";
}
public static class ModelCatalog
{
    public static ModelSelection Default(AppSettings settings)
    {
        settings = AiProfileCatalog.Normalize(settings);
        var p = settings.AiProfiles!.Value.FirstOrDefault(p => p.Id == settings.ActiveAiProfileId) ?? throw new InvalidOperationException("请先选择 AI 配置。");
        return Default(p);
    }
    public static ModelSelection Default(AiServiceProfile p)
    {
        var m = p.Models?.FirstOrDefault(m => m.Id == p.DefaultModelId) ?? throw new InvalidOperationException("请为此配置指定默认模型。");
        return Select(p,m,m.DefaultThinking);
    }
    public static ModelSelection Pending(AiServiceProfile p) => new(p.Id,Guid.Empty,new()) { ProfileName=p.Name };
    public static ModelSelection Select(AiServiceProfile p,AiModelProfile m,ThinkingChoice? thinking = null) => new(p.Id,m.Id,thinking ?? m.DefaultThinking) { ProfileName = p.Name,ModelName = m.Name };
    public static AiSettings Resolve(AppSettings settings,ModelSelection selection,bool requireAuxiliaryVerified,ServiceVerification verification)
    {
        settings = AiProfileCatalog.Normalize(settings);
        var p = settings.AiProfiles!.Value.FirstOrDefault(p => p.Id == selection.ProfileId) ?? throw new InvalidOperationException($"配置不可用：{selection.ProfileName}，请重新选择。");
        var m = p.Models?.FirstOrDefault(m => m.Id == selection.ModelId) ?? throw new InvalidOperationException($"模型不可用：{selection.ModelName}，请重新选择。");
        var readiness = ProfileCompleteness.Evaluate(p);
        if(!readiness.Complete) throw new InvalidOperationException(readiness.Reason);
        var capability = ThinkingCapability.For(m);
        if(!capability.Allows(selection.Thinking)) throw new InvalidOperationException("此模型不支持所选思考设置，请重新选择。");
        var ai = p.Settings with { Model = m.Name,Thinking = selection.Thinking,ThinkingCapability = capability };
        if(requireAuxiliaryVerified && !verification.CanUseInAuxiliary(settings,selection))
            throw new InvalidOperationException("此模型须通过文字及图片测试后才能在辅助模式中调用。");
        return ai;
    }
}
