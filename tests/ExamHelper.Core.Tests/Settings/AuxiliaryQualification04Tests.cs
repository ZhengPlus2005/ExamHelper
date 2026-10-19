using ExamHelper.Core.Interaction;
using ExamHelper.Core.Sessions;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public sealed class AuxiliaryQualification04Tests
{
    [Fact]
    public void ImageProofAloneDoesNotRestrictModelResolution()
    {
        var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Configured();
        settings = proof.RecordAiSuccess(settings,proof.AiFingerprint(settings));
        Assert.Equal("model-a",ModelCatalog.Resolve(settings,ModelCatalog.Default(settings)).Model);
        Assert.Equal("model-a",ModelCatalog.Resolve(settings,ModelCatalog.Default(settings)).Model);
    }
    [Theory]
    [InlineData(false,false,false)]
    [InlineData(true,false,false)]
    [InlineData(false,true,false)]
    [InlineData(true,true,true)]
    public void EachModelRequiresItsOwnTwoMatchingProofs(bool text,bool image,bool expected)
    {
        var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Configured();
        var p = settings.AiProfiles!.Value[0]; var a = p.Models!.Value[0]; var b = new AiModelProfile { Name="model-b" };
        settings = settings with { AiProfiles = [p with { Models=[a,b] }] };
        var fingerprint = proof.AiFingerprint(settings);
        settings = proof.RecordModelResult(settings,p.Id,a.Id,AiTestKind.Text,text,fingerprint);
        settings = proof.RecordModelResult(settings,p.Id,a.Id,AiTestKind.Image,image,fingerprint);
        Assert.Equal(expected,proof.CanUseInAuxiliary(settings,ModelCatalog.Select(p,a)));
        Assert.False(proof.CanUseInAuxiliary(settings,ModelCatalog.Select(p,b)));
        if(expected)
        {
            settings = proof.RecordModelResult(settings,p.Id,a.Id,AiTestKind.Text,false,fingerprint);
            var stored = settings.AiProfiles!.Value[0].Models!.Value[0];
            Assert.Null(stored.TextVerifiedFingerprint); Assert.Equal(fingerprint,stored.VerifiedFingerprint);
            Assert.False(proof.CanUseInAuxiliary(settings,ModelCatalog.Select(p,a)));
        }
    }
    [Fact]
    public void ChangedAddressOrRemovedModelRejectsLateResults()
    {
        var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Configured();
        var p = settings.AiProfiles!.Value[0]; var m = p.Models!.Value[0]; var fingerprint = proof.AiFingerprint(settings);
        var changed = settings with { AiProfiles=[p with { BaseAddress="https://changed.invalid/v1" }] };
        Assert.Throws<InvalidOperationException>(() => proof.RecordModelResult(changed,p.Id,m.Id,AiTestKind.Text,true,fingerprint));
        var removed = settings with { AiProfiles=[p with { Models=[],DefaultModelId=null }] };
        Assert.Throws<InvalidOperationException>(() => proof.RecordModelResult(removed,p.Id,m.Id,AiTestKind.Image,true,fingerprint));
    }
    [Fact]
    public async Task RecordingCanStillBeStoppedAfterSettingsLoseQualification()
    {
        var proof = new ServiceVerification(new byte[32]); var settings = TestSettings.Qualified(proof);
        var stub = new StubServices(); var store = new InMemorySessionStore();
        await using var coordinator = new SessionCoordinator(stub,stub,stub,stub,stub,() => settings,store,new ManualTimeProvider(),verification:proof);
        await coordinator.StartNewAsync(default); await coordinator.ExecuteAsync(AppCommand.ToggleRecording,default);
        Assert.Equal(SpeechState.Recording,coordinator.State.SpeechState);
        settings = settings with { Asr=settings.Asr with { DeviceId="changed" } };
        Assert.False(proof.IsAsrVerified(settings)); Assert.True(coordinator.CanExecute(AppCommand.ToggleRecording));
        await coordinator.ExecuteAsync(AppCommand.ToggleRecording,default);
        Assert.NotEqual(SpeechState.Recording,coordinator.State.SpeechState);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AuxiliaryChatIgnoresProofWhileRecordingStillRequiresItsOwnTest(bool hasVerifier)
    {
        var seed = new ServiceVerification(new byte[32]);
        var settings = hasVerifier ? TestSettings.Configured() : TestSettings.Qualified(seed); var proof = hasVerifier ? seed : null;
        var stub = new StubServices(); var store = new InMemorySessionStore();
        await using var coordinator = new SessionCoordinator(stub,stub,stub,stub,stub,() => settings,store,new ManualTimeProvider(),verification: proof);
        await coordinator.StartNewAsync(default);
        await coordinator.AppendSpeechResultAsync(new(coordinator.State.Id,coordinator.State.SpeechGeneration,Guid.NewGuid(),"question"),default);
        Assert.True(coordinator.CanExecute(AppCommand.Send));
        Assert.False(coordinator.CanExecute(AppCommand.ToggleRecording));
        Assert.True(coordinator.CanExecute(AppCommand.CaptureToDraft));
        await coordinator.ExecuteAsync(AppCommand.Send,default);await coordinator.WaitForIdleAsync(default);
        Assert.Equal("question",coordinator.State.Messages[0].Text);
        Assert.Equal(MessageStatus.Failed,coordinator.State.Messages[1].Status);
    }
}
