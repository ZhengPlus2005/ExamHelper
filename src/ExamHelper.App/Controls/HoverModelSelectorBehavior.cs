using System.Windows;
using System.Windows.Controls;
namespace ExamHelper.App.Controls;
public static class HoverModelSelectorBehavior
{
    public static void Attach(Button button,Action choose,Func<bool> available)
    {
        button.Focusable=false; var command=new DelegateCommand(choose,available); button.Command=command;
        DwellBehavior.SetDelay(button,1); DwellBehavior.SetCommand(button,command);
    }
}
