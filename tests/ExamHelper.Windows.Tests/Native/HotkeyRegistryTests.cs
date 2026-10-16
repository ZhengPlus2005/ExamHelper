using ExamHelper.Windows.Native;
using ExamHelper.Core.Settings;
using ExamHelper.Core.Interaction;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class HotkeyRegistryTests
{
    [Fact]
    public void ReportsDuplicatesBeforeRegistering()
    {
        var api = new FakeHotkeyApi(); using var registry = new HotkeyRegistry(123, api);
        var report = registry.Register([new(AppCommand.Send, 3, 0x73), new(AppCommand.Exit, 3, 0x73)]);
        Assert.False(report.Success); Assert.Empty(api.Keys);
    }
    [Fact]
    public void ReportsNativeConflictAndRollsBack()
    {
        var api = new FakeHotkeyApi { ConflictKey = 0x73 }; using var registry = new HotkeyRegistry(123, api);
        var report = registry.Register([new(AppCommand.CaptureToDraft, 3, 0x72), new(AppCommand.Send, 3, 0x73)]);
        Assert.False(report.Success); Assert.Single(report.Conflicts); Assert.Empty(api.Keys);
    }
    [Fact]
    public void UnregistersOnLeaveAndRoutesOnlyKnownIds()
    {
        var api = new FakeHotkeyApi(); using var registry = new HotkeyRegistry(123, api);
        Assert.True(registry.Register([new(AppCommand.Send, 3, 0x73)]).Success);
        AppCommand? actual = null; registry.CommandReceived += c => actual = c;
        registry.HandleMessage(api.Keys.Keys.Single()); Assert.Equal(AppCommand.Send, actual);
        registry.UnregisterAll(); Assert.Empty(api.Keys); actual = null;
        registry.HandleMessage(0x5000); Assert.Null(actual);
    }
    [Fact]
    public void EscapeOnlyDuringManipulation()
    {
        var api = new FakeHotkeyApi(); using var registry = new HotkeyRegistry(123, api);
        Assert.True(registry.SetManipulationEscape(true)); Assert.Single(api.Keys);
        Assert.True(registry.SetManipulationEscape(true)); Assert.Single(api.Keys);
        registry.SetManipulationEscape(false); Assert.Empty(api.Keys);
    }
    private sealed class FakeHotkeyApi : IHotkeyApi
    {
        public uint ConflictKey; public Dictionary<int, uint> Keys = [];
        public bool Register(nint hwnd, int id, uint modifiers, uint key, out int error)
        {
            error = key == ConflictKey ? 1409 : 0; if (error != 0) return false;
            Keys.Add(id, key); return true;
        }
        public bool Unregister(nint hwnd, int id) => Keys.Remove(id);
    }
}
