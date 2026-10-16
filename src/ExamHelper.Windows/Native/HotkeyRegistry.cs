using System.Runtime.InteropServices;
using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Native;
public sealed record HotkeyConflict(HotkeyBinding Binding, int ErrorCode, string Reason);
public sealed record HotkeyRegistrationReport(bool Success, IReadOnlyList<HotkeyConflict> Conflicts);
public interface IHotkeyApi
{
    bool Register(nint hwnd, int id, uint modifiers, uint key, out int error);
    bool Unregister(nint hwnd, int id);
}
public sealed class HotkeyRegistry(nint hwnd, IHotkeyApi? api = null) : IDisposable
{
    private const int FirstId = 0x5000, EscapeId = 0x5FFF;
    private readonly IHotkeyApi _api = api ?? new Win32HotkeyApi();
    private readonly Dictionary<int, AppCommand> _registered = [];
    private bool _escape;
    public event Action<AppCommand>? CommandReceived;
    public event Action? EscapePressed;
    public HotkeyRegistrationReport Register(IReadOnlyList<HotkeyBinding> bindings)
    {
        UnregisterAll();
        var duplicates = bindings.GroupBy(b => (b.Modifiers & ~0x4000u, b.VirtualKey)).Where(g => g.Count() > 1).SelectMany(g => g)
            .Concat(bindings.GroupBy(b => b.Command).Where(g => g.Count() > 1).SelectMany(g => g)).Distinct().ToArray();
        if (duplicates.Length > 0) return new(false, duplicates.Select(b => new HotkeyConflict(b, 0, "应用内重复绑定")).ToArray());
        foreach (var binding in bindings)
        {
            var id = FirstId + _registered.Count;
            if (!_api.Register(hwnd, id, binding.Modifiers | 0x4000, binding.VirtualKey, out var error))
            {
                UnregisterAll(); return new(false, [new(binding, error, "系统快捷键注册失败，已回滚本次注册")]);
            }
            _registered[id] = binding.Command;
        }
        return new(true, []);
    }
    public bool SetManipulationEscape(bool active)
    {
        if (active == _escape) return true;
        if (active) { _escape = _api.Register(hwnd, EscapeId, 0x4000, 0x1B, out _); return _escape; }
        var result = _api.Unregister(hwnd, EscapeId); _escape = false; return result;
    }
    public bool HandleMessage(int id)
    {
        if (id == EscapeId && _escape) { EscapePressed?.Invoke(); return true; }
        if (!_registered.TryGetValue(id, out var command)) return false;
        CommandReceived?.Invoke(command); return true;
    }
    public void UnregisterAll()
    {
        SetManipulationEscape(false);
        foreach (var id in _registered.Keys) _api.Unregister(hwnd, id);
        _registered.Clear();
    }
    public void Dispose() => UnregisterAll();
}
internal sealed class Win32HotkeyApi : IHotkeyApi
{
    public bool Register(nint hwnd, int id, uint modifiers, uint key, out int error)
    {
        var success = RegisterHotKey(hwnd, id, modifiers, key); error = success ? 0 : Marshal.GetLastWin32Error(); return success;
    }
    public bool Unregister(nint hwnd, int id) => UnregisterHotKey(hwnd, id);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(nint hwnd, int id);
}
