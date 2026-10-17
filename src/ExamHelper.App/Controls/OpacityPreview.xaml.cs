using System.Windows;
using System.Windows.Controls;
namespace ExamHelper.App.Controls;
public partial class OpacityPreview : UserControl
{
    public static readonly DependencyProperty BackgroundOpacityProperty = DependencyProperty.Register(nameof(BackgroundOpacity),typeof(double),typeof(OpacityPreview),new PropertyMetadata(0.85));
    public double BackgroundOpacity { get => (double)GetValue(BackgroundOpacityProperty); set => SetValue(BackgroundOpacityProperty,value); }
    public OpacityPreview() => InitializeComponent();
}
