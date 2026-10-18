using System.IO;
using ExamHelper.Windows.Persistence;
using ExamHelper.App.Composition;
using ExamHelper.App.Overlay;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class FreshStartup04Tests
{
    [Fact]
    public void ProductionDataRootUsesIndependent04Series()
    {
        var expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ExamHelper", "0.4");
        Assert.Equal(expected, AppDataPaths.Root);
    }
    [Fact]
    public async Task InjectedStartupDoesNotReadOrModifyLegacyData()
    {
        var root = Path.Combine(Path.GetTempPath(),"ExamHelper-start04-"+Guid.NewGuid().ToString("N"));
        var old = Path.Combine(root,"ExamHelper"); Directory.CreateDirectory(old);
        var sentinel = Path.Combine(old,"settings.json"); await File.WriteAllTextAsync(sentinel,"legacy sentinel");
        try
        {
            var layout = AppDataPaths.Resolve(root);
            await using(var app = await AppComposition.CreateAsync(_ => new Host(),layout))
            {
                Assert.Empty(app.CurrentSettings.AiProfiles!.Value);
                Assert.Equal(Path.Combine(root,"ExamHelper","0.4"),app.Paths.Root);
                Assert.True(File.Exists(layout.Database));
                await app.SettingsStore.SaveAsync(app.CurrentSettings,default);
            }
            await using(var reopened = await AppComposition.CreateAsync(_ => new Host(),layout))
                Assert.Empty(reopened.CurrentSettings.AiProfiles!.Value);
            Assert.Equal("legacy sentinel",await File.ReadAllTextAsync(sentinel));
            Assert.True(File.Exists(layout.Settings));
        }
        finally { Directory.Delete(root,true); }
    }
    private sealed class Host : IOverlayHost
    {
        public Task<ProtectionResult> OpenAsync(CancellationToken ct) => Task.FromResult(new ProtectionResult(true,0,17));
        public void Show() { }
        public void Close() { }
    }
}
