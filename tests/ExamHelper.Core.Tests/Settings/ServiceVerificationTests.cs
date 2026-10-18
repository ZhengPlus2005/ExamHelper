using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class ServiceVerificationTests
{
    private readonly ServiceVerification _verification = new(Enumerable.Range(0,32).Select(x => (byte)x).ToArray());
    private AppSettings Verified()
    {
        var settings = TestSettings.Configured(new() { BaseUri = new("https://api.moonshot.cn/v1"),SecretFingerprint = _verification.SecretDigest("ai-key") },new() { SecretFingerprint = _verification.SecretDigest("asr-key") });
        settings = _verification.RecordAiSuccess(settings,_verification.AiFingerprint(settings));
        return _verification.RecordAsrSuccess(settings,_verification.AsrFingerprint(settings));
    }
    [Fact]
    public void AiEditInvalidatesOnlyAi()
    {
        var settings = Verified(); Assert.True(_verification.CanStart(settings));
        settings = settings with { Ai = settings.Ai with { Model = "another" } };
        Assert.False(_verification.IsAiVerified(settings)); Assert.True(_verification.IsAsrVerified(settings));
        Assert.False(_verification.CanStart(settings));
    }
    [Fact]
    public void AsrDeviceEditInvalidatesOnlyAsr()
    {
        var settings = Verified(); settings = settings with { Asr = settings.Asr with { DeviceId = "new-device" } };
        Assert.False(_verification.IsAsrVerified(settings)); Assert.True(_verification.IsAiVerified(settings));
    }
    [Fact]
    public void AppearanceAndPromptDoNotInvalidate()
    {
        var settings = Verified() with { DefaultCodeLanguage = "Python",DirectCapturePrompt = "变化",Ui = new() { Opacity = 0.5 } };
        Assert.True(_verification.CanStart(settings));
    }
    [Fact]
    public void SecretContentChangeWithSameIdAndStaleTestCannotPassNewConfig()
    {
        var settings = Verified(); var old = _verification.AiFingerprint(settings);
        settings = settings with { Ai = settings.Ai with { SecretFingerprint = _verification.SecretDigest("changed-key") } };
        Assert.False(_verification.IsAiVerified(settings)); Assert.Equal("ai",settings.Ai.SecretId);
        Assert.Throws<InvalidOperationException>(() => _verification.RecordAiSuccess(settings,old));
    }
    [Fact]
    public void NormalizedEndpointIsEquivalentAndEmptyKeysCannotBeVerified()
    {
        var settings = Verified(); settings = settings with { Ai = settings.Ai with { BaseUri = new("https://api.moonshot.cn/v1/chat/completions/") } };
        Assert.True(_verification.CanStart(settings));
        var empty = new AppSettings();
        Assert.False(_verification.CanStart(empty));
        Assert.Throws<InvalidOperationException>(() => _verification.RecordAiSuccess(empty,_verification.AiFingerprint(empty)));
    }
}