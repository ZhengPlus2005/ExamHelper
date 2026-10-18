using System.IO;
using System.Text;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Settings;
using ExamHelper.TestSupport;
using Xunit;
namespace ExamHelper.Windows.Tests.Settings;
public class SettingsStoreTests
{
    [Fact]
    public async Task RoundTripsEncryptedCurrentUserSecretWithoutPlaintextFiles()
    {
        var root = Path.Combine(Path.GetTempPath(),"ExamHelper-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var secretStore = new UserSecretStore(Path.Combine(root,"Secrets"));
            const string secret = "test-sensitive-secret-请勿明文";
            await secretStore.SetAsync("ai",secret,default); Assert.Equal(secret,await secretStore.GetAsync("ai",default));
            var verification = new ServiceVerification(new byte[32]);
            var settings = TestSettings.Configured(new() { SecretFingerprint = verification.SecretDigest(secret) });
            var store = new JsonSettingsStore(Path.Combine(root,"settings.json"));
            await store.SaveAsync(settings,default); var loaded = await store.LoadAsync(default);
            Assert.Equal(AiProfileCatalog.Normalize(settings).Ai,loaded.Ai); Assert.Equal(settings.DirectCapturePrompt,loaded.DirectCapturePrompt);
            foreach (var path in Directory.EnumerateFiles(root,"*",SearchOption.AllDirectories))
                Assert.DoesNotContain(secret,Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
    [Fact]
    public async Task CorruptSettingsArePreservedAndReportedAsRecoverableError()
    {
        var root = Path.Combine(Path.GetTempPath(),"ExamHelper-settings-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root,"settings.json"); await File.WriteAllTextAsync(path,"{broken");
            var error = await Assert.ThrowsAsync<ServiceFailure>(() => new JsonSettingsStore(path).LoadAsync(default));
            Assert.Equal(FailureCategory.Storage,error.Category); Assert.Equal("{broken",await File.ReadAllTextAsync(path));
        }
        finally { Directory.Delete(root,true); }
    }
    [Fact]
    public async Task InvalidEndpointDoesNotOverwriteExistingSettings()
    {
        var root = Path.Combine(Path.GetTempPath(),"ExamHelper-settings-" + Guid.NewGuid().ToString("N"));
        try
        {
            var path = Path.Combine(root,"settings.json"); var store = new JsonSettingsStore(path);
            await store.SaveAsync(new(),default); var original = await File.ReadAllTextAsync(path);
            await Assert.ThrowsAsync<ArgumentException>(() => store.SaveAsync(TestSettings.Configured(new() { BaseUri = new("https://example.com/api?key=unsafe") }),default));
            Assert.Equal(original,await File.ReadAllTextAsync(path));
        }
        finally { if(Directory.Exists(root)) Directory.Delete(root,true); }
    }
}