namespace ExamHelper.Core.Settings;
public sealed record ProfileReadiness(bool Complete,Uri? Endpoint,string? Reason);
public static class ProfileCompleteness
{
    public static ProfileReadiness Evaluate(AiServiceProfile profile)
    {
        if(!Uri.TryCreate(profile.BaseAddress.Trim(),UriKind.Absolute,out var endpoint) ||
           endpoint.Scheme is not ("https" or "http") || endpoint.UserInfo.Length > 0 || endpoint.Query.Length > 0 || endpoint.Fragment.Length > 0)
            return new(false,null,"请填写有效的 API 地址。");
        if(string.IsNullOrWhiteSpace(profile.Name)) return new(false,endpoint,"请填写配置名称。");
        if(string.IsNullOrWhiteSpace(profile.Settings.SecretFingerprint)) return new(false,endpoint,"请填写 API 密钥。");
        var models = profile.Models ?? [];
        if(models.IsDefaultOrEmpty || models.Any(m => string.IsNullOrWhiteSpace(m.Name)) || !models.Any(m => m.Id == profile.DefaultModelId))
            return new(false,endpoint,"请添加模型并指定默认模型。");
        return new(true,endpoint,null);
    }
}
