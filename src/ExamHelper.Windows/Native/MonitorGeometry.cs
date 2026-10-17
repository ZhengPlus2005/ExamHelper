using System.Runtime.InteropServices;
using ExamHelper.Core.Common;
using ExamHelper.Core.Settings;
namespace ExamHelper.Windows.Native;
public sealed record MonitorArea(PhysicalWindowPlacement Bounds,PhysicalWindowPlacement WorkArea);
public static class MonitorGeometry
{
    public static MonitorArea ForWindow(nint hwnd)
    {
        if(hwnd == 0 || !IsWindow(hwnd)) throw new ServiceFailure(FailureCategory.InvalidPayload,"无法定位辅助窗口所属的显示器。");
        return Read(MonitorFromWindow(hwnd,2));
    }
    public static MonitorArea ForPlacement(PhysicalWindowPlacement placement)
    {
        var rect = ToRect(placement); return Read(MonitorFromRect(ref rect,2));
    }
    public static PhysicalWindowPlacement WindowBounds(nint hwnd)
    {
        if(!GetWindowRect(hwnd,out var rect)) throw Failure("无法读取辅助窗口位置。");
        return FromRect(rect);
    }
    public static void SetBounds(nint hwnd,PhysicalWindowPlacement bounds)
    {
        if(!SetWindowPos(hwnd,0,bounds.X,bounds.Y,bounds.Width,bounds.Height,0x0014)) throw Failure("无法更新辅助窗口位置。");
    }
    public static void Move(nint hwnd,int x,int y)
    {
        if(!SetWindowPos(hwnd,0,x,y,0,0,0x0015)) throw Failure("无法移动辅助窗口。");
    }
    private static MonitorArea Read(nint monitor)
    {
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if(monitor == 0 || !GetMonitorInfo(monitor,ref info)) throw Failure("无法读取显示器信息。");
        return new(FromRect(info.Monitor),FromRect(info.Work));
    }
    private static PhysicalWindowPlacement FromRect(Rect r) => new(r.Left,r.Top,r.Right - r.Left,r.Bottom - r.Top);
    private static Rect ToRect(PhysicalWindowPlacement p) => new() { Left = p.X,Top = p.Y,Right = checked(p.X + p.Width),Bottom = checked(p.Y + p.Height) };
    private static ServiceFailure Failure(string message) => new(FailureCategory.Transport,message,Marshal.GetLastWin32Error().ToString());
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor,Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll")] private static extern nint MonitorFromRect(ref Rect rect,uint flags);
    [DllImport("user32.dll",CharSet = CharSet.Auto,SetLastError = true)] private static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [DllImport("user32.dll",SetLastError = true)] private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    [DllImport("user32.dll",SetLastError = true)] private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint hwnd);
}
