using System.Windows;
using System.Windows.Controls;
using ExamHelper.Core.Markdown;
namespace ExamHelper.App.Controls;
public sealed class MarkdownView : UserControl
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(MarkdownView),new PropertyMetadata("",Changed));
    public static readonly DependencyProperty AuxiliaryProperty=DependencyProperty.Register(nameof(Auxiliary),typeof(bool),typeof(MarkdownView),new PropertyMetadata(false,Changed));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty,value); }
    public bool Auxiliary { get => (bool)GetValue(AuxiliaryProperty); set => SetValue(AuxiliaryProperty,value); }
    public MarkdownView() { Focusable=false; Render(); }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e) => ((MarkdownView)d).Render();
    private void Render() => Content=MarkdownWpfRenderer.Render(MarkdownPipelineFactory.Parse(Text),Auxiliary);
}
