using ExamHelper.Core.Interaction;
using ExamHelper.Core.Settings;
using Xunit;
namespace ExamHelper.Core.Tests.Interaction;
public class ScreenPlacementTests
{
    [Fact]
    public void KeepsNegativeMonitorCoordinatesAndFitsDisconnectedPlacement()
    {
        var secondary = new PhysicalWindowPlacement(-1920,0,1920,1040);
        var original = new PhysicalWindowPlacement(-1810,50,640,720);
        Assert.Equal(original,ScreenPlacement.Clamp(original,secondary));
        var restored = ScreenPlacement.Clamp(new(5000,3000,640,720),new(0,0,1920,1040));
        Assert.Equal(new PhysicalWindowPlacement(1280,320,640,720),restored);
    }
    [Fact]
    public void SmallerWorkAreaShrinksOversizedWindowWithoutDpiResampling()
    {
        Assert.Equal(new PhysicalWindowPlacement(-400,-200,300,200),ScreenPlacement.Clamp(new(-900,-800,900,800),new(-400,-200,300,200)));
    }
}
