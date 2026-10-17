using System.Windows.Input;
namespace ExamHelper.App.Common;
public sealed class AsyncDelegateCommand(Func<Task> execute,Func<bool> canExecute,Action<Exception> error) : ICommand
{
    private bool _running;
    public bool CanExecute(object? parameter) => !_running && canExecute();
    public async void Execute(object? parameter)
    {
        if(!CanExecute(parameter)) return;
        _running = true; Raise();
        try { await execute(); } catch(Exception failure) { error(failure); }
        finally { _running = false; Raise(); }
    }
    public event EventHandler? CanExecuteChanged;
    public void Raise() => CanExecuteChanged?.Invoke(this,EventArgs.Empty);
}