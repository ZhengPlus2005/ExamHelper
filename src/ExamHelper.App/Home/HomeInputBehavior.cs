using System.Windows.Controls;
using System.Windows.Input;
namespace ExamHelper.App.Home;
public sealed class HomeInputBehavior
{
    private bool _composing;
    public bool IsComposing => _composing;
    public static bool ShouldSend(Key key,ModifierKeys modifiers,bool composing) => key == Key.Enter && modifiers == ModifierKeys.None && !composing;
    public HomeInputBehavior(TextBox input,Func<Task> send,Action<Exception> report)
    {
        TextCompositionManager.AddPreviewTextInputStartHandler(input,(_,_) => _composing = true);
        TextCompositionManager.AddPreviewTextInputUpdateHandler(input,(_,_) => _composing = true);
        TextCompositionManager.AddPreviewTextInputHandler(input,(_,e) => { if(e.TextComposition.CompositionText.Length == 0) _composing = false; });
        input.PreviewKeyDown += async (_,e) =>
        {
            if(!ShouldSend(e.Key,Keyboard.Modifiers,_composing)) return;
            e.Handled = true; try { await send(); } catch(Exception error) { report(error); }
        };
        input.LostKeyboardFocus += (_,_) => _composing = false;
    }
}
