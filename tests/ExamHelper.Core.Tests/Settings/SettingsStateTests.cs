using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Settings;
public class SettingsStateTests
{
    [Fact] public void LateCredentialRefreshCannotOverwriteNewerSavedConfiguration()
    {
        var original=new AppSettings(); var state=new SettingsState(original); var newer=original with { DefaultCodeLanguage="Python" };
        state.Publish(newer); var result=state.PublishRefresh(original,original with { Ai=original.Ai with { SecretFingerprint="old" } });
        Assert.Same(newer,result); Assert.Same(newer,state.Current);
    }
    [Fact] public void UnchangedConfigurationAcceptsItsCredentialRefresh()
    {
        var original=new AppSettings(); var state=new SettingsState(original); var refreshed=original with { Ai=original.Ai with { SecretFingerprint="current" } };
        Assert.Same(refreshed,state.PublishRefresh(original,refreshed));
    }
}
