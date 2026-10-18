using System.IO;
using ExamHelper.Core.Common;
using ExamHelper.Windows.Settings;
using Xunit;
namespace ExamHelper.Windows.Tests.Settings;
public class ModelSettingsRevisionTests
{
    [Theory]
    [InlineData("{\"FormatVersion\":2}")]
    [InlineData("{\"FormatVersion\":3,\"Ui\":{\"Opacity\":0.65}}")]
    [InlineData("{\"FormatVersion\":99}")]
    [InlineData("{}")]
    public async Task ForeignSettingsAreReportedWithoutImportOrOverwrite(string json)
    {
        var path = Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".json");
        try
        {
            await File.WriteAllTextAsync(path,json);
            await Assert.ThrowsAsync<ServiceFailure>(() => new JsonSettingsStore(path).LoadAsync(default));
            Assert.Equal(json,await File.ReadAllTextAsync(path));
        }
        finally { File.Delete(path); }
    }
}
