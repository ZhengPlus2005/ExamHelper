using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace ExamHelper.App.Controls;
public partial class AnswerView : UserControl
{
    public static readonly DependencyProperty TextProperty=DependencyProperty.Register(nameof(Text),typeof(string),typeof(AnswerView),new PropertyMetadata("",Changed));
    public static readonly DependencyProperty TitleProperty=DependencyProperty.Register(nameof(Title),typeof(string),typeof(AnswerView),new PropertyMetadata("",HeaderChanged));
    public static readonly DependencyProperty IsFinalProperty=DependencyProperty.Register(nameof(IsFinal),typeof(bool),typeof(AnswerView),new PropertyMetadata(false));
    public static readonly DependencyProperty IsMarkdownProperty=DependencyProperty.Register(nameof(IsMarkdown),typeof(bool),typeof(AnswerView),new PropertyMetadata(true,Changed));
    public static readonly DependencyProperty TimingTextProperty=DependencyProperty.Register(nameof(TimingText),typeof(string),typeof(AnswerView),new PropertyMetadata("",TimingChanged));
    public string TimingText { get=>(string)GetValue(TimingTextProperty);set=>SetValue(TimingTextProperty,value); }
    private static void TimingChanged(DependencyObject d,DependencyPropertyChangedEventArgs e) { if(((AnswerView)d).TimingLabel is { } label)label.Text=(string)e.NewValue; }
    private static void HeaderChanged(DependencyObject d,DependencyPropertyChangedEventArgs e) { if(((AnswerView)d).Header is { } label)label.Text=(string)e.NewValue; }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty,value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty,value); }
    public bool IsFinal { get => (bool)GetValue(IsFinalProperty); set => SetValue(IsFinalProperty,value); }
    public bool IsMarkdown { get => (bool)GetValue(IsMarkdownProperty); set => SetValue(IsMarkdownProperty,value); }
    public AnswerView() { InitializeComponent(); Render(); }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs e) { if(((AnswerView)d).Segments is not null) ((AnswerView)d).Render(); }
    private void Render()
    {
        Header.Text=Title; Segments.Children.Clear();
        Segments.Children.Add(IsMarkdown ? new MarkdownView { Auxiliary=true,Text=Text } : new TextBlock { Text=Text,Foreground=Brushes.White,TextWrapping=TextWrapping.Wrap,FontSize=14,Focusable=false });
    }
}
