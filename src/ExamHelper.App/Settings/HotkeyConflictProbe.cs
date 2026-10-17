using System.Windows.Interop;
using ExamHelper.Core.Settings;
using ExamHelper.Windows.Native;
namespace ExamHelper.App.Settings;
public static class HotkeyConflictProbe
{
    public static string? Check(IReadOnlyList<HotkeyBinding> bindings)
    {
        using var source = new HwndSource(new HwndSourceParameters("ExamHelper Shortcut Probe") { ParentWindow = new nint(-3),Width = 0,Height = 0,WindowStyle = 0 });
        using var registry = new HotkeyRegistry(source.Handle);
        var report = registry.Register(bindings);
        return report.Success ? null : "快捷键冲突：" + string.Join("；",report.Conflicts.Select(c => CommandNames.Get(c.Binding.Command) + " " + HotkeyEditor.Format(c.Binding) + $"（代码 {c.ErrorCode}）"));
    }
}