namespace ExamHelper.Windows.Native;

public readonly record struct ProtectionResult(bool Success, int ErrorCode, uint EffectiveAffinity);
public interface IWindowProtectionApi
{
    bool Set(nint hwnd, uint value, out int error);
    bool Get(nint hwnd, out uint value, out int error);
}
public sealed class WindowProtection(IWindowProtectionApi? api = null)
{
    private readonly IWindowProtectionApi _api = api ?? new Win32WindowProtectionApi();
    public ProtectionResult ApplyAndVerify(nint hwnd)
    {
        if (!_api.Set(hwnd, 0x11, out var error)) return new(false, error, 0);
        if (!_api.Get(hwnd, out var value, out error)) return new(false, error, 0);
        return new(value == 0x11, value == 0x11 ? 0 : error, value);
    }
}
internal sealed class Win32WindowProtectionApi : IWindowProtectionApi
{
    public bool Set(nint hwnd, uint value, out int error)
    {
        var result = NativeMethods.SetWindowDisplayAffinity(hwnd, value);
        error = result ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        return result;
    }
    public bool Get(nint hwnd, out uint value, out int error)
    {
        var result = NativeMethods.GetWindowDisplayAffinity(hwnd, out value);
        error = result ? 0 : System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        return result;
    }
}
