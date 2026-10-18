using System.Runtime.InteropServices;
namespace ExamHelper.Windows.Native;
public static class ApplicationIdentity
{
    public const string Id="ExamHelper.Desktop.0.4";
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    public static void Apply()=>Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Id));
}
