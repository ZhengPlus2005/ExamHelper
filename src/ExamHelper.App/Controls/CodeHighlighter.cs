using System.Windows.Documents;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
namespace ExamHelper.App.Controls;
public static class CodeHighlighter
{
    public static IReadOnlyList<Run> Highlight(string code,string language,bool dark)
    {
        var extension=language.Trim().ToLowerInvariant() switch { "c++" or "cpp" or "cxx" => ".cpp","c" => ".c","c#" or "cs" or "csharp" => ".cs","py" or "python" => ".py","js" or "javascript" => ".js","ts" or "typescript" => ".ts","java" => ".java","json" => ".json","sql" => ".sql","xml" or "html" => ".xml","sh" or "bash" => ".sh",_ => "" };
        var definition=extension.Length==0 ? null : HighlightingManager.Instance.GetDefinitionByExtension(extension);
        if(definition is null) return [Make(code,null,dark)];
        var document=new TextDocument(code); using var highlighter=new DocumentHighlighter(document,definition); var runs=new List<Run>();
        for(var n=1;n<=document.LineCount;n++)
        {
            var line=document.GetLineByNumber(n); var sections=highlighter.HighlightLine(n).Sections;
            var boundaries=sections.SelectMany(s => new[] { s.Offset,s.Offset+s.Length }).Append(line.Offset).Append(line.EndOffset).Where(v => v>=line.Offset && v<=line.EndOffset).Distinct().Order().ToArray();
            for(var i=0;i<boundaries.Length-1;i++)
            {
                var start=boundaries[i]; var end=boundaries[i+1]; if(end<=start) continue;
                var color=sections.LastOrDefault(s => s.Offset<=start && s.Offset+s.Length>=end && s.Color.Foreground is not null)?.Color.Foreground?.GetBrush(null);
                runs.Add(Make(document.GetText(start,end-start),color,dark));
            }
            if(line.DelimiterLength>0) runs.Add(Make(document.GetText(line.EndOffset,line.DelimiterLength),null,dark));
        }
        return runs;
    }
    private static Run Make(string text,Brush? color,bool dark)
    {
        if(dark && color is SolidColorBrush solid)
        {
            var c=solid.Color; color=new SolidColorBrush(Color.FromRgb((byte)(128+c.R/2),(byte)(128+c.G/2),(byte)(128+c.B/2)));
        }
        return new Run(text) { Foreground=color ?? (dark ? Brushes.White : Brushes.Black),FontFamily=new FontFamily("Consolas") };
    }
}
