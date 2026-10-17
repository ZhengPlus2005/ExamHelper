using System.IO;
using System.Net.Http;
using System.Text;
using ExamHelper.Core.Audio;
using ExamHelper.Core.Settings;
using ExamHelper.TestSupport;
using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.Settings;
public class ServiceTestRunnerTests
{
    [Theory]
    [InlineData("EH7Q9",true)]
    [InlineData("another answer",false)]
    public async Task ImagePassRequiresActualMarkerAndReturnsTestedConfigFingerprint(string answer,bool expected)
    {
        using var handler = new FakeHttpHandler((_,_) => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        { Content = new StringContent("data: {\"choices\":[{\"delta\":{\"content\":\"" + answer + "\"},\"finish_reason\":\"stop\"}]}\n\ndata: [DONE]\n\n",Encoding.UTF8,"text/event-stream") }));
        using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("ai","test",default);
        var verification = new ServiceVerification(new byte[32]); var settings = await verification.RefreshSecretDigestsAsync(new(),secrets,default);
        var runner = new ServiceTestRunner(http,secrets,verification,[1,2]);
        var result = await runner.TestImageAsync(settings,default);
        Assert.Equal(expected,result.Passed); Assert.Equal(verification.AiFingerprint(settings),result.Fingerprint);
    }
    [Theory]
    [InlineData("中文 and English",true)]
    [InlineData("",false)]
    public async Task SpeechResultRequiresHumanConfirmationAndEmptyResultCannotPass(string transcript,bool canConfirm)
    {
        var file = Path.GetTempFileName(); await File.WriteAllBytesAsync(file,[1]);
        try
        {
            using var handler = new FakeHttpHandler((_,_) => Task.FromResult(FakeHttpHandler.Json(System.Text.Json.JsonSerializer.Serialize(new { choices = new[] { new { finish_reason = "stop",message = new { content = transcript } } } }))));
            using var http = new HttpClient(handler); var secrets = new InMemorySecretStore(); await secrets.SetAsync("asr","test",default);
            var verification = new ServiceVerification(new byte[32]); var settings = await verification.RefreshSecretDigestsAsync(new(),secrets,default);
            var result = await new ServiceTestRunner(http,secrets,verification,[1]).TestSpeechAsync(new(Guid.NewGuid(),file,TimeSpan.FromSeconds(1)),settings,default);
            Assert.False(result.Passed); Assert.Equal(canConfirm,result.RequiresConfirmation);
            if(canConfirm) Assert.Equal(transcript,result.Text);
        }
        finally { File.Delete(file); }
    }
}