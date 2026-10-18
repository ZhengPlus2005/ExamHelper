using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ExamHelper.Windows.Images;
namespace ExamHelper.App.Home;
public interface IClipboardAccess { IDataObject? GetDataObject(); }
public sealed class WindowsClipboardAccess:IClipboardAccess { public IDataObject? GetDataObject()=>Clipboard.GetDataObject(); }
public sealed class HomePasteBehavior:IDisposable
{
    private readonly TextBox _input;
    private readonly HomeDraftController _drafts;
    private readonly IClipboardAccess _clipboard;
    private readonly Action<Exception> _report;
    public HomePasteBehavior(TextBox input,HomeDraftController drafts,IClipboardAccess clipboard,Action<Exception> report)
    {
        _input=input;_drafts=drafts;_clipboard=clipboard;_report=report;
        CommandManager.AddPreviewCanExecuteHandler(input,CanExecute);CommandManager.AddPreviewExecutedHandler(input,Execute);
    }
    private void CanExecute(object sender,CanExecuteRoutedEventArgs e)
    {
        if(e.Command!=ApplicationCommands.Paste)return;
        e.CanExecute=_input.IsEnabled && !_input.IsReadOnly;e.Handled=true;
    }
    private async void Execute(object sender,ExecutedRoutedEventArgs e)
    {
        if(e.Command!=ApplicationCommands.Paste)return;
        if(!_input.IsEnabled || _input.IsReadOnly) { e.Handled=true;return; }
        try
        {
            var data=_clipboard.GetDataObject();if(data is null)return;
            var payload=new ClipboardImageReader().Read(data);if(payload is null)return;
            e.Handled=true;
            if(payload.Bitmap is { } bitmap)await _drafts.ImportBitmapAsync(bitmap,CancellationToken.None);
            else await _drafts.ImportFilesAsync(payload.Files,CancellationToken.None);
        }
        catch(Exception error) { e.Handled=true;_report(error); }
    }
    public void Dispose()
    { CommandManager.RemovePreviewCanExecuteHandler(_input,CanExecute);CommandManager.RemovePreviewExecutedHandler(_input,Execute); }
}
