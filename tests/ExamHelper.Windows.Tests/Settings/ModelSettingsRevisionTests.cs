using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.Settings;
public class ModelSettingsRevisionTests
{
    [Theory]
    [InlineData("{\"FormatVersion\":2}",0.85)]
    [InlineData("{\"FormatVersion\":2,\"Ui\":{\"Width\":650}}",0.85)]
    [InlineData("{\"FormatVersion\":2,\"Ui\":{\"Opacity\":0.65}}",0.65)]
    [InlineData("{\"FormatVersion\":3}",0.5)]
    public async Task MissingOpacityInOldFilesKeepsOldDefault(string json,double expected)
    {
        var path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".json");
        try { await System.IO.File.WriteAllTextAsync(path,json); Assert.Equal(expected,(await new JsonSettingsStore(path).LoadAsync(default)).Ui.Opacity); }
        finally { System.IO.File.Delete(path); }
    }
}
