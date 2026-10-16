namespace ExamHelper.Windows.Native;
public readonly record struct FocusResult(bool Success, string? Error = null);
public interface IFocusApi
{
    nint GetForeground();
    bool IsWindow(nint hwnd);
    InputState ReadInput(nint hwnd);
    bool Focus(nint hwnd);
    bool EnterEnglish(nint hwnd);
    bool RestoreInput(nint hwnd, InputState state);
}
public sealed class FocusLease(IFocusApi? api = null)
{
    private readonly IFocusApi _api = api ?? new Win32FocusApi();
    private nint _target, _overlay;
    private InputState _state, _overlayState;
    private bool _active;
    public bool IsActive => _active;
    public FocusResult Begin(nint overlayHwnd)
    {
        if (_active) return new(true);
        _target = _api.GetForeground(); _overlay = overlayHwnd;
        if (_target == overlayHwnd || !_api.IsWindow(_target)) return new(false, "没有有效的原答题窗口，请先切回目标窗口。");
        _state = _api.ReadInput(_target); _overlayState = _api.ReadInput(_overlay);
        if (!_api.Focus(_overlay)) return new(false, "Windows 未允许切换到输入窗口。");
        if (!_api.EnterEnglish(_overlay))
        {
            _api.RestoreInput(_overlay, _overlayState); _api.Focus(_target);
            return new(false, "英文输入状态初始化失败。");
        }
        _active = true; return new(true);
    }
    public FocusResult End()
    {
        if (!_active) return new(true);
        _active = false;
        var overlayRestored = _api.IsWindow(_overlay) && _api.RestoreInput(_overlay, _overlayState);
        if (!_api.IsWindow(_target)) return new(false, "原答题窗口已关闭，草稿仍保留。");
        var restored = _api.RestoreInput(_target, _state);
        var focused = _api.Focus(_target);
        return new(overlayRestored && restored && focused, overlayRestored && restored && focused ? null : "焦点或输入状态未能完全恢复，草稿仍保留。");
    }
}
