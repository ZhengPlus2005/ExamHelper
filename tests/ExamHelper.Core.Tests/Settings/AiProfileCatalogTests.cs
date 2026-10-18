using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class AiProfileCatalogTests
{
    [Fact]
    public void CurrentCustomCommandsAreNotReinterpretedAsLegacyCommands()
    {
        var settings = new AppSettings { Hotkeys = new() { Bindings = [new(ExamHelper.Core.Interaction.AppCommand.Exit,3,0x7B)] } };
        var normalized = AiProfileCatalog.Normalize(settings);
        Assert.Equal(4,normalized.FormatVersion);
        Assert.Equal(10,(int)ExamHelper.Core.Interaction.AppCommand.Exit);
        Assert.Equal(ExamHelper.Core.Interaction.AppCommand.Exit,Assert.Single(normalized.Hotkeys.Bindings).Command);
    }
    private readonly ServiceVerification _verification = new(new byte[32]);
    [Fact]
    public void AbsentCatalogNeverImportsSingleProviderOrItsEvidence()
    {
        var old = new AppSettings { AiProfiles = null,Ai = new() { Model = "old-model",SecretFingerprint = "digest" },Evidence = new("verified-ai","verified-asr") };
        var normalized = AiProfileCatalog.Normalize(old);
        Assert.Empty(normalized.AiProfiles!.Value);
        Assert.Null(normalized.ActiveAiProfileId);
        Assert.Null(normalized.Ai.BaseUri);
        Assert.False(_verification.IsAiVerified(normalized));
        Assert.Equal("verified-asr",normalized.Evidence.AsrFingerprint);
        Assert.Equal(normalized,AiProfileCatalog.Normalize(normalized));
    }
    [Fact]
    public void SelectionKeepsIndependentEvidenceAndInactiveEditsDoNotInvalidateActive()
    {
        var a = TestSettings.Configured(new() { SecretFingerprint = "a" }).AiProfiles!.Value[0] with { Name = "A" };
        var b = TestSettings.Configured(new() { SecretId = "b",SecretFingerprint = "b",Model = "other" }).AiProfiles!.Value[0] with { Name = "B" };
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
