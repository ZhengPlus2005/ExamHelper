using System.Windows;
using System.Windows.Input;
using ExamHelper.Core.Interaction;
namespace ExamHelper.App.Controls;
public sealed class WindowManipulationBehavior
{
    public WindowManipulationBehavior(Window window, FrameworkElement moveHandle, FrameworkElement resizeHandle, WindowInteractionSession session)
    {
        DwellBehavior.SetCommand(moveHandle, new DelegateCommand(() =>
        {
            var left = window.Left; var top = window.Top;
            session.Start(InteractionMode.Moving, d =>
            {
                var area = SystemParameters.WorkArea;
                window.Left = Math.Clamp(left + d.DeltaX, area.Left, Math.Max(area.Left, area.Right - window.ActualWidth));
                window.Top = Math.Clamp(top + d.DeltaY, area.Top, Math.Max(area.Top, area.Bottom - window.ActualHeight));
            });
        }));
        DwellBehavior.SetCommand(resizeHandle, new DelegateCommand(() =>
        {
            var width = window.ActualWidth; var height = window.ActualHeight;
            session.Start(InteractionMode.Resizing, d =>
            {
                var area = SystemParameters.WorkArea;
                window.Width = Math.Clamp(width + d.DeltaX, Math.Min(window.MinWidth, area.Width), Math.Max(window.MinWidth, area.Right - window.Left));
                window.Height = Math.Clamp(height + d.DeltaY, Math.Min(window.MinHeight, area.Height), Math.Max(window.MinHeight, area.Bottom - window.Top));
            });
        }));
    }
}
public sealed class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) execute(); }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
