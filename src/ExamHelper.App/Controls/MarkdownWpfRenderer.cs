using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using ExamHelper.App.Common;
namespace ExamHelper.App.Controls;
public static partial class MarkdownWpfRenderer
{
    public static FrameworkElement Render(MarkdownDocument document,bool auxiliary) => Container(document,auxiliary);
    private static StackPanel Container(ContainerBlock blocks,bool auxiliary)
    {
        var panel=new StackPanel { Focusable=false };
        foreach(var block in blocks) panel.Children.Add(Block(block,auxiliary)); return panel;
    }
    private static FrameworkElement Block(Block block,bool auxiliary)
    {
        switch(block)
        {
            case HeadingBlock heading:
                var title=Text(auxiliary); title.FontSize=Math.Max(14,25-heading.Level*2); title.FontWeight=FontWeights.Bold; title.Margin=new Thickness(0,10,0,7); AddInlines(title.Inlines,heading.Inline,auxiliary); return title;
            case ParagraphBlock paragraph:
                var text=Text(auxiliary); AddInlines(text.Inlines,paragraph.Inline,auxiliary); return text;
            case CodeBlock code: return Code(code,auxiliary);
            case Table table: return TableView(table,auxiliary);
            case ListBlock list:
                var items=new StackPanel { Margin=new Thickness(2,2,0,7),Focusable=false };
                var number=int.TryParse(Convert.ToString(list.OrderedStart,System.Globalization.CultureInfo.InvariantCulture),out var start) ? start : 1;
                foreach(var item in list.OfType<ListItemBlock>())
                {
                    var row=new Grid { Focusable=false }; row.ColumnDefinitions.Add(new() { Width=new GridLength(28) }); row.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
                    var marker=Text(auxiliary); marker.Text=list.IsOrdered ? number++.ToString()+"." : "•"; row.Children.Add(marker);
                    var child=Container(item,auxiliary); Grid.SetColumn(child,1); row.Children.Add(child); items.Children.Add(row);
                }
                return items;
            case QuoteBlock quote:
                return new Border { Child=Container(quote,auxiliary),BorderBrush=auxiliary ? Brushes.SlateGray : Brushes.LightSteelBlue,BorderThickness=new Thickness(3,0,0,0),Padding=new Thickness(10,2,4,2),Margin=new Thickness(0,4,0,8),Focusable=false };
            case ThematicBreakBlock: return new Border { Height=1,Background=Brushes.SlateGray,Margin=new Thickness(0,10,0,10),Focusable=false };
            case ContainerBlock nested: return Container(nested,auxiliary);
            default: return Text(auxiliary);
        }
    }
    private static TextBlock Text(bool auxiliary) => new() { Foreground=auxiliary ? Brushes.White : new SolidColorBrush(Color.FromRgb(25,35,49)),FontSize=14,TextWrapping=TextWrapping.Wrap,LineHeight=22,Margin=new Thickness(0,2,0,6),Focusable=false };
    private static FrameworkElement Code(CodeBlock block,bool auxiliary)
    {
        var language=block is FencedCodeBlock fenced ? fenced.Info?.Split(' ',StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "" : "";
        var source=block.Lines.ToString(); var stack=new StackPanel { Focusable=false }; var header=new DockPanel { Margin=new Thickness(0,0,0,5),Focusable=false };
        var copy=new Button { Content="复制",FontSize=11,Focusable=!auxiliary,Padding=new Thickness(8,3,8,3),Margin=new Thickness(4,0,0,3) };
        var command=new AsyncDelegateCommand(() => { Clipboard.SetText(source); copy.Content="已复制"; return Task.CompletedTask; },() => true,_ => copy.Content="复制失败，请重试");
        copy.Command=command; if(auxiliary) DwellBehavior.SetCommand(copy,command);
        DockPanel.SetDock(copy,Dock.Right); header.Children.Add(copy); var label=Text(auxiliary); label.Text=language.Length>0 ? language : "代码"; label.FontSize=11; header.Children.Add(label); stack.Children.Add(header);
        var code=Text(auxiliary); code.TextWrapping=TextWrapping.NoWrap; code.FontFamily=new FontFamily("Consolas"); code.LineHeight=20; code.Margin=new Thickness(0);
        foreach(var run in CodeHighlighter.Highlight(source,language,auxiliary)) code.Inlines.Add(run);
        var codeScroll=new ScrollViewer { Content=code,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false };ChatWheelRouting.Attach(codeScroll);stack.Children.Add(codeScroll);
        return new Border { Background=auxiliary ? new SolidColorBrush(Color.FromArgb(190,8,17,30)) : new SolidColorBrush(Color.FromRgb(241,244,248)),CornerRadius=new CornerRadius(6),Padding=new Thickness(10),Margin=new Thickness(0,5,0,10),Child=stack,Focusable=false };
    }
    private static FrameworkElement TableView(Table table,bool auxiliary)
    {
        var grid=new Grid { Margin=new Thickness(0,5,0,10),Focusable=false };
        var columns=table.OfType<TableRow>().Select(r => r.Count).DefaultIfEmpty(1).Max();
        for(var i=0;i<columns;i++) grid.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        var rowIndex=0;
        foreach(var row in table.OfType<TableRow>())
        {
            grid.RowDefinitions.Add(new() { Height=GridLength.Auto }); var col=0;
            foreach(var cell in row.OfType<TableCell>())
            {
                var border=new Border { Child=Container(cell,auxiliary),Padding=new Thickness(8,4,8,4),BorderThickness=new Thickness(0.5),BorderBrush=Brushes.SlateGray,Background=row.IsHeader ? (auxiliary ? new SolidColorBrush(Color.FromArgb(100,68,90,117)) : Brushes.AliceBlue) : Brushes.Transparent,Focusable=false };
                if(row.IsHeader) System.Windows.Documents.TextElement.SetFontWeight(border,FontWeights.SemiBold);
                Grid.SetRow(border,rowIndex); Grid.SetColumn(border,col++); grid.Children.Add(border);
            }
            rowIndex++;
        }
        return grid;
    }
}
