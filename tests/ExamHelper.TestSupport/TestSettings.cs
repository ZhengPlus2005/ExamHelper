using ExamHelper.Core.Settings;
namespace ExamHelper.TestSupport;
public static class TestSettings
{
    public static AppSettings Qualified(ServiceVerification proof)
    {
        var settings = Configured(new() { SecretFingerprint=proof.SecretDigest("ai") },new() { SecretFingerprint=proof.SecretDigest("asr") });
        var p = settings.AiProfiles!.Value[0]; var m = p.Models!.Value[0]; var fp = proof.AiFingerprint(settings);
        settings = proof.RecordModelResult(settings,p.Id,m.Id,AiTestKind.Text,true,fp);
        settings = proof.RecordModelResult(settings,p.Id,m.Id,AiTestKind.Image,true,fp);
        return proof.RecordAsrSuccess(settings,proof.AsrFingerprint(settings));
    }
    public static AppSettings Configured() => Configured(new AiSettings());
    public static AppSettings Configured(AiSettings ai,AsrSettings? asr = null)
    {
        ai = ai with { BaseUri = ai.BaseUri ?? new("https://example.invalid/v1"),Model = string.IsNullOrWhiteSpace(ai.Model) ? "model-a" : ai.Model,
            SecretFingerprint = string.IsNullOrEmpty(ai.SecretFingerprint) ? "test-secret-digest" : ai.SecretFingerprint };
        var model = new AiModelProfile { Name = ai.Model,DefaultThinking = ai.Thinking };
        var profile = new AiServiceProfile { Name = "测试配置",BaseAddress = ai.BaseUri.ToString(),Settings = ai,Models = [model],DefaultModelId = model.Id };
        return AiProfileCatalog.Normalize(new() { AiProfiles = [profile],ActiveAiProfileId = profile.Id,Asr = asr ?? new() });
    }
}
