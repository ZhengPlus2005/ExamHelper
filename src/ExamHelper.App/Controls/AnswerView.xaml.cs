using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExamHelper.Core.Ai;
namespace ExamHelper.App.Controls;
public partial class AnswerView : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text),typeof(string),typeof(AnswerView),new PropertyMetadata("",Changed));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title),typeof(string),typeof(AnswerView),new PropertyMetadata("",Changed));
    public static readonly DependencyProperty IsFinalProperty = DependencyProperty.Register(nameof(IsFinal),typeof(bool),typeof(AnswerView),new PropertyMetadata(false,Changed));
    private readonly List<(AnswerSegmentKind Kind,string Language,TextBlock Text)> _parts = [];
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty,value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty,value); }
    public bool IsFinal { get => (bool)GetValue(IsFinalProperty); set => SetValue(IsFinalProperty,value); }
    public AnswerView() { InitializeComponent(); Render(); }
    private static void Changed(DependencyObject d,DependencyPropertyChangedEventArgs args) { if(((AnswerView)d).Segments is not null) ((AnswerView)d).Render(); }
    private void Render()
    {
        Header.Text = Title;
        var blocks = CodeBlockParser.Parse(Text ?? "",IsFinal);
        bool rebuild = blocks.Count != _parts.Count || blocks.Where((b,i) => i >= _parts.Count || b.Kind != _parts[i].Kind || b.Language != _parts[i].Language).Any();
        if(rebuild)
        {
            Segments.Children.Clear(); _parts.Clear();
            foreach(var block in blocks)
            {
                var text = new TextBlock { Foreground = Brushes.White,TextWrapping = TextWrapping.Wrap,FontSize = 14,Focusable = false };
                if(block.Kind == AnswerSegmentKind.Code)
                {
                    text.FontFamily = new FontFamily("Consolas"); var stack = new StackPanel();
                    stack.Children.Add(new TextBlock { Text = block.Language.Length > 0 ? block.Language : "代码",Foreground = new SolidColorBrush(Color.FromRgb(160,190,225)),Margin = new Thickness(0,0,0,5) });
                    stack.Children.Add(text); Segments.Children.Add(new Border { Background = new SolidColorBrush(Color.FromArgb(150,7,15,28)),CornerRadius = new CornerRadius(5),Padding = new Thickness(10),Margin = new Thickness(0,4,0,8),Child = stack });
                }
                else Segments.Children.Add(text);
                _parts.Add((block.Kind,block.Language,text));
            }
        }
        for(int i = 0;i < blocks.Count;i++) _parts[i].Text.Text = blocks[i].Text;
    }
}