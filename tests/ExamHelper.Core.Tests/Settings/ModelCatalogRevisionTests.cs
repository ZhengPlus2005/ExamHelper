using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class ModelCatalogRevisionTests
{
    private readonly ServiceVerification _verification = new(new byte[32]);
    [Fact] public void FreshDefaultsAndLegacyCommandMigrationAreDistinct()
    {
        Assert.Equal(0.5,new AppSettings().Ui.Opacity);
        var old = new AppSettings { FormatVersion = 2,Hotkeys = new() { Bindings = [new(AppCommand.HideOverlay,3,0x7B),new(AppCommand.Send,2,0x7B),new(AppCommand.ToggleVisibility,3,0x78)] } };
        var migrated = AiProfileCatalog.Normalize(old);
        Assert.Equal(3,migrated.FormatVersion);
        Assert.DoesNotContain(migrated.Hotkeys.Bindings,b => b.Command == AppCommand.HideOverlay);
        Assert.Contains(migrated.Hotkeys.Bindings,b => b.Command == AppCommand.Send && b.VirtualKey == 0x7B);
        Assert.Contains(migrated.Hotkeys.Bindings,b => b.Command == AppCommand.ToggleVisibility);
    }
    [Fact] public void LegacyImportsOneModelWithOldThinkingButNewModelUsesServiceDefault()
    {
        var old = new AppSettings { FormatVersion = 2,Ai = new() { SecretFingerprint = "test" } };
        old = old with { Evidence = new(_verification.AiFingerprint(old)) };
        var migrated = AiProfileCatalog.Normalize(old);
        var p = Assert.Single(migrated.AiProfiles!.Value); var m = Assert.Single(p.Models!.Value);
        Assert.Equal(p.DefaultModelId,m.Id); Assert.Equal(ThinkingKind.Disabled,m.DefaultThinking.Kind);
        Assert.Equal(old.Evidence.AiFingerprint,m.VerifiedFingerprint);
        Assert.Equal(ThinkingKind.ServiceDefault,new AiModelProfile().DefaultThinking.Kind);
    }
    [Fact] public void ResolveRequiresMatchingModelProofAndNeverFallsBack()
    {
        var a = new AiModelProfile { Name = "kimi-k3" }; var b = new AiModelProfile { Name = "kimi-k2.6" };
        var p = new AiServiceProfile { Models = [a,b],DefaultModelId = a.Id,Settings = new() { SecretFingerprint = "test" } };
        var s = AiProfileCatalog.Normalize(new() { AiProfiles = [p],ActiveAiProfileId = p.Id });
        var choice = new ModelSelection(p.Id,a.Id,new(ThinkingKind.Effort,"high"));
        Assert.Throws<InvalidOperationException>(() => ModelCatalog.Resolve(s,choice,true,_verification));
        var proof = _verification.AiFingerprint(s with { Ai = p.Settings with { Model = a.Name } });
        p = p with { Models = [a with { VerifiedFingerprint = proof },b] };
        s = s with { AiProfiles = [p] };
        Assert.Equal("kimi-k3",ModelCatalog.Resolve(s,choice,true,_verification).Model);
        Assert.Throws<InvalidOperationException>(() => ModelCatalog.Resolve(s,choice with { ModelId = b.Id },true,_verification));
        Assert.Equal("kimi-k2.6",ModelCatalog.Resolve(s,choice with { ModelId = b.Id,Thinking = new(ThinkingKind.ServiceDefault) },false,_verification).Model);
        Assert.Throws<InvalidOperationException>(() => ModelCatalog.Resolve(s,choice with { ProfileId = Guid.NewGuid() },false,_verification));
    }
}
