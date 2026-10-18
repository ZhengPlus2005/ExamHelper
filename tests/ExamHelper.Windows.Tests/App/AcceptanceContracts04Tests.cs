using System.IO;
using System.Text.Json;
using System.Windows.Threading;
using Xunit;
namespace ExamHelper.Windows.Tests.App;
public sealed class AcceptanceContracts04Tests
{
    [Fact] public async Task IsolatedHomeWorkflowUsesProductPathsAndFakeNetworkOnly()
    {
        await StaTest.RunAsync(()=>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());var folder=Path.Combine(Path.GetTempPath(),"ExamHelper-accept04-"+Guid.NewGuid().ToString("N"));
            try
            {
                var running=ExamHelper.InteractionLab.HomeInteractionVerification.RunAsync(folder);var watch=System.Diagnostics.Stopwatch.StartNew();while(!running.IsCompleted && watch.Elapsed<TimeSpan.FromSeconds(12)) { StaTest.Pump();Thread.Sleep(3); }running.GetAwaiter().GetResult();
                using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(folder,"result.json")));Assert.True(json.RootElement.GetProperty("Passed").GetBoolean());Assert.Equal(0,json.RootElement.GetProperty("NetworkRequests").GetInt32());Assert.False(json.RootElement.GetProperty("UserSettingsRead").GetBoolean());
                Assert.True(File.Exists(Path.Combine(folder,"conversation.html")));Assert.True(File.Exists(Path.Combine(folder,"saved-original.png")));
            }
            finally { if(Directory.Exists(folder))Directory.Delete(folder,true); }
        });
    }
}
