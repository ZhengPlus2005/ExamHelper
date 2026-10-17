using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class AiProfileCatalogTests
{
    private readonly ServiceVerification _verification = new(new byte[32]);
    [Fact]
    public void LegacyMigrationRetainsEndpointSecretIdentityAndEvidence()
    {
        var old = new AppSettings { Ai = new() { SecretId = "ai",Model = "old-model",SecretFingerprint = "digest" },Evidence = new("verified-ai","verified-asr") };
        var migrated = AiProfileCatalog.Normalize(old);
        var profile = Assert.Single(migrated.AiProfiles!.Value);
        Assert.Equal(old.Ai,profile.Settings); Assert.Equal("verified-ai",profile.VerifiedFingerprint);
        Assert.Equal(profile.Id,migrated.ActiveAiProfileId);
        Assert.Equal(migrated,AiProfileCatalog.Normalize(migrated));
    }
    [Fact]
    public void SelectionKeepsIndependentEvidenceAndInactiveEditsDoNotInvalidateActive()
    {
        var a = new AiServiceProfile { Name = "A",Settings = new() { SecretFingerprint = "a" } };
        var b = new AiServiceProfile { Name = "B",Settings = new() { SecretId = "b",SecretFingerprint = "b",Model = "other" } };
        var settings = AiProfileCatalog.Normalize(new() { AiProfiles = [a,b],ActiveAiProfileId = a.Id });
        settings = _verification.RecordAiSuccess(settings,_verification.AiFingerprint(settings));
        Assert.True(_verification.IsAiVerified(settings));
        b = b with { Settings = b.Settings with { Model = "changed" } };
        settings = AiProfileCatalog.Normalize(settings with { AiProfiles = [settings.AiProfiles!.Value[0],b] });
        Assert.True(_verification.IsAiVerified(settings));
        var switched = AiProfileCatalog.Activate(settings,b.Id);
        Assert.False(_verification.IsAiVerified(switched));
        Assert.True(_verification.IsAiVerified(AiProfileCatalog.Activate(switched,a.Id)));
    }
    [Fact]
    public void ExplicitEmptyAndDisabledCatalogNeverReuseLegacyVerification()
    {
        var empty = AiProfileCatalog.Normalize(new() { AiProfiles = [],Ai = new() { SecretFingerprint = "old" },Evidence = new("old") });
        Assert.Null(empty.ActiveAiProfileId); Assert.False(_verification.IsAiVerified(empty));
        Assert.Empty(empty.AiProfiles!.Value);
        Assert.Throws<ArgumentException>(() => AiProfileCatalog.Activate(empty,Guid.NewGuid()));
    }
}
