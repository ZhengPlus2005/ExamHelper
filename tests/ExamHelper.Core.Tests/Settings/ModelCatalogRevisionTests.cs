using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class ModelCatalogRevisionTests
{
    private readonly ServiceVerification _verification = new(new byte[32]);
    [Fact] public void FreshDefaultsExposeOnlyCurrentHotkeysAndNoProvider()
    {
        var settings = AiProfileCatalog.Normalize(new());
        Assert.Equal(0.5,settings.Ui.Opacity);
        Assert.Equal(4,settings.FormatVersion);
        Assert.DoesNotContain(settings.Hotkeys.Bindings,b => b.Command == AppCommand.HideOverlay);
        Assert.Contains(settings.Hotkeys.Bindings,b => b.Command == AppCommand.ToggleVisibility && b.VirtualKey == 0x78);
        Assert.Empty(settings.AiProfiles!.Value);
    }
    [Fact] public void EmptyModelCatalogDoesNotImportOldModelOrProof()
    {
        var profile = new AiServiceProfile { Settings = new() { Model = "kimi-k2.6",SecretFingerprint = "test" },VerifiedFingerprint = "old-proof" };
        var settings = AiProfileCatalog.Normalize(new() { AiProfiles = [profile],ActiveAiProfileId = profile.Id });
        Assert.Empty(settings.AiProfiles!.Value[0].Models!.Value);
        Assert.Null(settings.AiProfiles.Value[0].DefaultModelId);
        Assert.Null(settings.Evidence.AiFingerprint);
        Assert.Equal("",settings.Ai.Model);
        Assert.Equal(ThinkingKind.ServiceDefault,new AiModelProfile().DefaultThinking.Kind);
    }
    [Fact] public void ResolveIgnoresSelfTestsAndNeverFallsBack()
    {
        var a = new AiModelProfile { Name = "kimi-k3" }; var b = new AiModelProfile { Name = "kimi-k2.6" };
        var p = new AiServiceProfile { Name = "P",BaseAddress = "https://example.invalid/v1",Models = [a,b],DefaultModelId = a.Id,Settings = new() { BaseUri = new("https://example.invalid/v1"),SecretFingerprint = "test" } };
        var s = AiProfileCatalog.Normalize(new() { AiProfiles = [p],ActiveAiProfileId = p.Id });
        var choice = new ModelSelection(p.Id,a.Id,new(ThinkingKind.Effort,"high"));
        Assert.Equal("kimi-k3",ModelCatalog.Resolve(s,choice).Model);
        var proof = _verification.AiFingerprint(s with { Ai = p.Settings with { Model = a.Name } });
        p = p with { Models = [a with { TextVerifiedFingerprint = proof,VerifiedFingerprint = proof },b] };
        s = s with { AiProfiles = [p] };
        Assert.Equal("kimi-k3",ModelCatalog.Resolve(s,choice).Model);
        Assert.Throws<InvalidOperationException>(() => ModelCatalog.Resolve(s,choice with { ModelId = b.Id }));
        Assert.Equal("kimi-k2.6",ModelCatalog.Resolve(s,choice with { ModelId = b.Id,Thinking = new(ThinkingKind.ServiceDefault) }).Model);
        Assert.Throws<InvalidOperationException>(() => ModelCatalog.Resolve(s,choice with { ProfileId = Guid.NewGuid() }));
    }
}
