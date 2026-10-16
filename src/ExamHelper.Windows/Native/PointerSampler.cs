using System.ComponentModel;
using System.Runtime.InteropServices;
using ExamHelper.Core.Interaction;
namespace ExamHelper.Windows.Native;
public static class PointerSampler
{
    public static PointerSample Read()
    {
        if (!GetCursorPos(out var point)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return new(point.X, point.Y, TimeProvider.System.GetTimestamp());
    }
    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool GetCursorPos(out Point point);
}
