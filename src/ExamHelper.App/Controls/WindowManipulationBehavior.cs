using System.Windows.Interop;
using System.Windows.Media;
using ExamHelper.Windows.Native;
using ExamHelper.Core.Settings;
using System.Windows;
using System.Windows.Input;
using ExamHelper.Core.Interaction;
namespace ExamHelper.App.Controls;
public sealed class WindowManipulationBehavior
{
    public WindowManipulationBehavior(Window window, FrameworkElement moveHandle, FrameworkElement resizeHandle, WindowInteractionSession session)
    {
        if(session.UsesPhysicalPointer) { AttachPhysical(window,moveHandle,resizeHandle,session); return; }
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
    private static void AttachPhysical(Window window,FrameworkElement moveHandle,FrameworkElement resizeHandle,WindowInteractionSession session)
    {
        var handle = new WindowInteropHelper(window);
        DwellBehavior.SetCommand(moveHandle,new DelegateCommand(() =>
        {
            var origin = MonitorGeometry.WindowBounds(handle.Handle);
            session.Start(InteractionMode.Moving,d => MonitorGeometry.Move(handle.Handle,origin.X + (int)Math.Round(d.DeltaX),origin.Y + (int)Math.Round(d.DeltaY)));
        }));
        DwellBehavior.SetCommand(resizeHandle,new DelegateCommand(() =>
        {
            var origin = MonitorGeometry.WindowBounds(handle.Handle);
            var dpi = VisualTreeHelper.GetDpi(window);
            session.Start(InteractionMode.Resizing,d =>
            {
                var area = MonitorGeometry.ForWindow(handle.Handle).WorkArea;
                var minWidth = Math.Min((int)Math.Ceiling(window.MinWidth * dpi.DpiScaleX),area.Width);
                var minHeight = Math.Min((int)Math.Ceiling(window.MinHeight * dpi.DpiScaleY),area.Height);
                var width = Math.Clamp(origin.Width + (int)Math.Round(d.DeltaX),minWidth,Math.Max(minWidth,area.X + area.Width - origin.X));
                var height = Math.Clamp(origin.Height + (int)Math.Round(d.DeltaY),minHeight,Math.Max(minHeight,area.Y + area.Height - origin.Y));
                MonitorGeometry.SetBounds(handle.Handle,new(origin.X,origin.Y,width,height));
            });
        }));
        session.ActiveChanged += active =>
        {
            if(active || !window.IsVisible) return;
            var bounds = MonitorGeometry.WindowBounds(handle.Handle);
            var area = MonitorGeometry.ForWindow(handle.Handle).WorkArea;
            var dpi = VisualTreeHelper.GetDpi(window);
            window.MinWidth = Math.Min(window.MinWidth,area.Width / dpi.DpiScaleX);
            window.MinHeight = Math.Min(window.MinHeight,area.Height / dpi.DpiScaleY);
            MonitorGeometry.SetBounds(handle.Handle,ScreenPlacement.Clamp(bounds,area));
        };
    }
}
public sealed class DelegateCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) { if (CanExecute(parameter)) execute(); }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
}
