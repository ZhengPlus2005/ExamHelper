using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace ExamHelper.Core.Settings;
public sealed record ServiceEvidence(string? AiFingerprint = null,string? AsrFingerprint = null);
public sealed class ServiceVerification
{
    private readonly byte[] _hmac;
    public ServiceVerification(byte[] hmacKey)
    {
        if (hmacKey.Length < 32) throw new ArgumentException("验证摘要密钥至少需要 32 字节。",nameof(hmacKey));
        _hmac = hmacKey.ToArray();
    }
    public string SecretDigest(string? value) => string.IsNullOrEmpty(value) ? "" : Convert.ToHexString(HMACSHA256.HashData(_hmac,Encoding.UTF8.GetBytes(value)));
    public string AiFingerprint(AppSettings settings) => Hash(new { Endpoint = Endpoint(settings.Ai.BaseUri),settings.Ai.Model,settings.Ai.ProviderKind,settings.Ai.SecretId,settings.Ai.SecretFingerprint });
    public string AsrFingerprint(AppSettings settings) => Hash(new { Endpoint = Endpoint(settings.Asr.BaseUri),settings.Asr.Model,settings.Asr.SecretId,settings.Asr.SecretFingerprint,settings.Asr.DeviceId });
    public bool IsAiVerified(AppSettings settings) => (settings.AiProfiles is null || settings.ActiveAiProfileId is not null) && settings.Ai.SecretFingerprint.Length > 0 && settings.Evidence.AiFingerprint == AiFingerprint(settings);
    public bool IsAsrVerified(AppSettings settings) => settings.Asr.SecretFingerprint.Length > 0 && settings.Evidence.AsrFingerprint == AsrFingerprint(settings);
    public bool CanStart(AppSettings settings) => IsAiVerified(settings) && IsAsrVerified(settings);
    public AppSettings RecordAiSuccess(AppSettings settings,string testedFingerprint)
    {
        if (settings.Ai.SecretFingerprint.Length == 0 || AiFingerprint(settings) != testedFingerprint)
            throw new InvalidOperationException("AI 配置已变化或密钥为空，请重新测试。");
        return settings with
        {
            Evidence = settings.Evidence with { AiFingerprint = testedFingerprint },
            AiProfiles = settings.AiProfiles?.Select(p => p.Id == settings.ActiveAiProfileId ? p with { VerifiedFingerprint = testedFingerprint,Models = p.Models?.Select(m => m.Name == settings.Ai.Model ? m with { VerifiedFingerprint = testedFingerprint } : m).ToImmutableArray() } : p).ToImmutableArray()
        };
    }
    public AppSettings RecordAsrSuccess(AppSettings settings,string testedFingerprint)
    {
        if (settings.Asr.SecretFingerprint.Length == 0 || AsrFingerprint(settings) != testedFingerprint)
            throw new InvalidOperationException("语音配置已变化或密钥为空，请重新测试。");
        return settings with { Evidence = settings.Evidence with { AsrFingerprint = testedFingerprint } };
    }
    public async Task<AppSettings> RefreshSecretDigestsAsync(AppSettings settings,ISecretStore secrets,CancellationToken ct)
    {
        var asr = await secrets.GetAsync(settings.Asr.SecretId,ct).ConfigureAwait(false);
        if(settings.AiProfiles is null)
        {
            var ai = await secrets.GetAsync(settings.Ai.SecretId,ct).ConfigureAwait(false);
            return settings with { Ai = settings.Ai with { SecretFingerprint = SecretDigest(ai) },Asr = settings.Asr with { SecretFingerprint = SecretDigest(asr) } };
        }
        var profiles = ImmutableArray.CreateBuilder<AiServiceProfile>();
        foreach(var profile in settings.AiProfiles.Value)
        {
            var key = await secrets.GetAsync(profile.Settings.SecretId,ct).ConfigureAwait(false);
            profiles.Add(profile with { Settings = profile.Settings with { SecretFingerprint = SecretDigest(key) } });
        }
        return AiProfileCatalog.Normalize(settings with { AiProfiles = profiles.ToImmutable(),Asr = settings.Asr with { SecretFingerprint = SecretDigest(asr) } });
    }

    private string Hash<T>(T value) => Convert.ToHexString(HMACSHA256.HashData(_hmac,JsonSerializer.SerializeToUtf8Bytes(value)));
    private static string Endpoint(Uri? uri)
    {
        if(uri is null) return "";
        var text = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return text.EndsWith("/chat/completions",StringComparison.OrdinalIgnoreCase) ? text[..^"/chat/completions".Length] : text;
    }
}
