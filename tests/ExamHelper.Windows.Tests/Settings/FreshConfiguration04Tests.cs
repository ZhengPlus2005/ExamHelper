using System.IO;
using ExamHelper.App.Settings;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.Settings;
public sealed class FreshConfiguration04Tests
{
    [Fact]
    public void NormalizationDoesNotRecreateLegacyProvider()
    {
        var settings = AiProfileCatalog.Normalize(new AppSettings());
        Assert.Empty(settings.AiProfiles!.Value);
        Assert.Null(settings.ActiveAiProfileId);
        Assert.Null(settings.Ai.BaseUri);
        Assert.Equal("", settings.Ai.Model);
        Assert.Equal(settings, AiProfileCatalog.Normalize(settings));
    }
    [Fact]
    public void NewProfileEditorHasNoInheritedModelOrAddress()
    {
        var editor = new AiProfileEditor(new AiServiceProfile(), "", new ServiceVerification(new byte[32]));
        Assert.Empty(editor.Models);
        Assert.Null(editor.SelectedModel);
        Assert.Equal("", editor.Name);
        Assert.Equal("", editor.BaseAddress);
    }
    [Fact]
    public void RepeatedModelAdditionDoesNotCreateBlankOrDuplicateRows()
    {
        var editor = new AiProfileEditor(new AiServiceProfile { Models = [] }, "", new ServiceVerification(new byte[32]));
        var first = editor.AddModel(new AiModelProfile { Name = "model-a" });
        var again = editor.AddModel(new AiModelProfile { Name = " model-a " });
        Assert.Same(first, again);
        Assert.Single(editor.Models);
        Assert.Throws<ArgumentException>(() => editor.AddModel(new AiModelProfile { Name = " " }));
        Assert.Equal("model-a", first.Name);
    }
    [Fact]
    public void NewModelInputDoesNotRenameSelectionOrChangeDefault()
    {
        var editor = new AiProfileEditor(new AiServiceProfile(), "", new ServiceVerification(new byte[32]));
        var first = editor.AddModel("model-a"); editor.DefaultModelId = first.Id;
        editor.NewModelId = " model-b ";
        Assert.Equal("model-a",first.Name);
        var second = editor.AddModel(editor.NewModelId);
        Assert.Equal("model-b",second.Name);
        Assert.Equal(first.Id,editor.DefaultModelId);
        Assert.Same(second,editor.AddModel("model-b"));
        Assert.Throws<ArgumentException>(() => editor.AddModel(" "));
        Assert.Equal(2,editor.Models.Count);
        Assert.Equal("model-a",editor.BuildProfile().Models!.Value[0].Name);
    }
    [Fact]
    public async Task CorruptModelEntryIsReportedWithoutOverwritingFile()
    {
        var path = Path.Combine(Path.GetTempPath(),"ExamHelper-corrupt04-"+Guid.NewGuid().ToString("N")+".json");
        const string json = "{\"FormatVersion\":4,\"AiProfiles\":[{\"Models\":[null]}]}";
        try
        {
            await File.WriteAllTextAsync(path,json);
            await Assert.ThrowsAsync<ExamHelper.Core.Common.ServiceFailure>(() => new JsonSettingsStore(path).LoadAsync(default));
            Assert.Equal(json,await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public async Task IncompleteProfileCanBeSavedWithoutCallingIt()
    {
        var root = Path.Combine(Path.GetTempPath(), "ExamHelper-fresh04-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new JsonSettingsStore(Path.Combine(root, "settings.json"));
            var profile = new AiServiceProfile { Name = "", BaseAddress = "https://", Settings = new() { BaseUri = null! }, Models = [] };
            await store.SaveAsync(new AppSettings { AiProfiles = [profile], ActiveAiProfileId = profile.Id }, default);
            var loaded = await store.LoadAsync(default);
            Assert.Equal("", loaded.AiProfiles!.Value[0].Name);
            Assert.Empty(loaded.AiProfiles.Value[0].Models!.Value);
            Assert.Null(loaded.Ai.BaseUri);
            Assert.Equal("https://", loaded.AiProfiles.Value[0].BaseAddress);
            Assert.False(ProfileCompleteness.Evaluate(loaded.AiProfiles.Value[0]).Complete);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
