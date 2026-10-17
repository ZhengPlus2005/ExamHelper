using System.Runtime.InteropServices;
using ExamHelper.Windows.Native;
using Xunit;
namespace ExamHelper.Windows.Tests.Native;
public class WindowsPlatformTests
{
    [Theory]
    [InlineData(22000,1,Architecture.X64,Architecture.X64,true)]
    [InlineData(19045,1,Architecture.X64,Architecture.X64,false)]
    [InlineData(26200,3,Architecture.X64,Architecture.X64,false)]
    [InlineData(26200,1,Architecture.Arm64,Architecture.X64,false)]
    [InlineData(26200,1,Architecture.X64,Architecture.X86,false)]
    public void OnlyWindows11WorkstationNativeX64IsSupported(int build,byte type,Architecture os,Architecture process,bool expected)
        => Assert.Equal(expected,WindowsPlatform.IsSupported(new(build,type,os,process)));
    [Fact]
    public void ReadsRealWindowsBuildAndArchitecture()
    {
        var current = WindowsPlatform.Current();
        Assert.True(current.Build >= 22000); Assert.Equal(Architecture.X64,current.ProcessArchitecture);
        Assert.True(WindowsPlatform.IsSupported(current));
    }
}