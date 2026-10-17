using System.Runtime.InteropServices;
namespace ExamHelper.Windows.Native;
public sealed record WindowsPlatformInfo(int Build,byte ProductType,Architecture OsArchitecture,Architecture ProcessArchitecture);
public static class WindowsPlatform
{
    public static bool IsSupported(WindowsPlatformInfo info) => info.Build >= 22000 && info.ProductType == 1 &&
        info.OsArchitecture == Architecture.X64 && info.ProcessArchitecture == Architecture.X64;
    public static WindowsPlatformInfo Current()
    {
        var version = new NativeVersion { Size = (uint)Marshal.SizeOf<NativeVersion>(),ServicePack = "" };
        if(RtlGetVersion(ref version) != 0) throw new InvalidOperationException("无法确认 Windows 版本。");
        return new((int)version.Build,version.ProductType,RuntimeInformation.OSArchitecture,RuntimeInformation.ProcessArchitecture);
    }
    [StructLayout(LayoutKind.Sequential,CharSet = CharSet.Unicode)]
    private struct NativeVersion
    {
        public uint Size,Major,Minor,Build,PlatformId;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst = 128)] public string ServicePack;
        public ushort ServicePackMajor,ServicePackMinor,SuiteMask;
        public byte ProductType,Reserved;
    }
    [DllImport("ntdll.dll",CharSet = CharSet.Unicode)] private static extern int RtlGetVersion(ref NativeVersion version);
}