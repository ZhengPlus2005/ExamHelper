using ExamHelper.Core.Settings;
namespace ExamHelper.Core.Interaction;
public static class ScreenPlacement
{
    public static PhysicalWindowPlacement Clamp(PhysicalWindowPlacement window,PhysicalWindowPlacement workArea)
    {
        if(window.Width <= 0 || window.Height <= 0 || workArea.Width <= 0 || workArea.Height <= 0) throw new ArgumentException("窗口或显示器尺寸无效。");
        var width = Math.Min(window.Width,workArea.Width); var height = Math.Min(window.Height,workArea.Height);
        return new((int)Math.Clamp((long)window.X,workArea.X,(long)workArea.X + workArea.Width - width),
            (int)Math.Clamp((long)window.Y,workArea.Y,(long)workArea.Y + workArea.Height - height),width,height);
    }
}
