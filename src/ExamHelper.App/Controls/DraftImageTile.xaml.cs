using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public partial class DraftImageTile:UserControl
{
    public static readonly DependencyProperty PreviewSourceProperty=DependencyProperty.Register(nameof(PreviewSource),typeof(ImageSource),typeof(DraftImageTile));
    public static readonly DependencyProperty PreviewCommandProperty=DependencyProperty.Register(nameof(PreviewCommand),typeof(ICommand),typeof(DraftImageTile));
    public static readonly DependencyProperty RemoveCommandProperty=DependencyProperty.Register(nameof(RemoveCommand),typeof(ICommand),typeof(DraftImageTile));
    public ImageSource? PreviewSource { get => (ImageSource?)GetValue(PreviewSourceProperty);set=>SetValue(PreviewSourceProperty,value); }
    public ICommand? PreviewCommand { get => (ICommand?)GetValue(PreviewCommandProperty);set=>SetValue(PreviewCommandProperty,value); }
    public ICommand? RemoveCommand { get => (ICommand?)GetValue(RemoveCommandProperty);set=>SetValue(RemoveCommandProperty,value); }
    public DraftImageTile()=>InitializeComponent();
}
