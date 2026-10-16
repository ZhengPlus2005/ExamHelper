namespace ExamHelper.Windows.Native;
public interface IInputRestoreApi
{
    bool RestoreLayout(nint hwnd, nint layout);
    nint GetContext(nint hwnd);
    bool SetConversion(nint context, uint conversion, uint sentence);
    bool SetOpen(nint context, bool open);
    void ReleaseContext(nint hwnd, nint context);
}
public sealed class InputStateRestorer(IInputRestoreApi api)
{
    public bool Restore(nint hwnd, InputState state)
    {
        if (!api.RestoreLayout(hwnd, state.Layout)) return false;
        var context = api.GetContext(hwnd);
        if (context == 0) return state.Conversion is null && state.Sentence is null && state.Open is null;
        try
        {
            var success = true;
            if (state.Conversion is uint conversion && state.Sentence is uint sentence)
                success &= api.SetConversion(context, conversion, sentence);
            if (state.Open is bool open) success &= api.SetOpen(context, open);
            return success;
        }
        finally { api.ReleaseContext(hwnd, context); }
    }
}
