using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Markdig.Syntax.Inlines;
using ExamHelper.Core.Markdown;
namespace ExamHelper.App.Controls;
public static partial class MarkdownWpfRenderer
{
    private static void AddInlines(InlineCollection target,ContainerInline? container,bool auxiliary)
    {
        for(var node=container?.FirstChild;node is not null;node=node.NextSibling)
        {
            switch(node)
            {
                case LiteralInline literal: target.Add(new Run(literal.Content.ToString())); break;
                case CodeInline code: target.Add(new Run(code.Content) { FontFamily=new FontFamily("Consolas"),Background=auxiliary ? new SolidColorBrush(Color.FromArgb(140,7,15,28)) : Brushes.WhiteSmoke }); break;
                case LineBreakInline line: if(line.IsHard) target.Add(new LineBreak()); else target.Add(new Run(" ")); break;
                case EmphasisInline emphasis:
                    var span=new Span(); if(emphasis.DelimiterCount>=2) span.FontWeight=FontWeights.Bold; else span.FontStyle=FontStyles.Italic;
                    AddInlines(span.Inlines,emphasis,auxiliary); target.Add(span); break;
                case LinkInline link:
                    if(!link.IsImage && MarkdownPipelineFactory.TrySafeLink(link.Url,out var uri))
                    {
                        var hyperlink=new Hyperlink { NavigateUri=uri,Focusable=!auxiliary,Foreground=auxiliary ? Brushes.LightSkyBlue : Brushes.RoyalBlue,Cursor=Cursors.Hand };
                        AddInlines(hyperlink.Inlines,link,auxiliary); hyperlink.RequestNavigate+=(_,e) => { e.Handled=true; OpenLink(e.Uri); }; target.Add(hyperlink);
                    }
                    else AddInlines(target,link,auxiliary);
                    break;
                case AutolinkInline auto:
                    if(MarkdownPipelineFactory.TrySafeLink(auto.Url,out var url))
                    {
                        var hyperlink=new Hyperlink(new Run(auto.Url)) { NavigateUri=url,Focusable=!auxiliary,Foreground=auxiliary ? Brushes.LightSkyBlue : Brushes.RoyalBlue };
                        hyperlink.RequestNavigate+=(_,e) => { e.Handled=true; OpenLink(e.Uri); }; target.Add(hyperlink);
                    }
                    else target.Add(new Run(auto.Url));
                    break;
                case HtmlEntityInline entity: target.Add(new Run(entity.Transcoded.ToString())); break;
                case ContainerInline nested: AddInlines(target,nested,auxiliary); break;
            }
        }
    }
    private static void OpenLink(Uri uri)
    {
        if(!MarkdownPipelineFactory.TrySafeLink(uri.AbsoluteUri,out var safe)) return;
        try { Process.Start(new ProcessStartInfo(safe!.AbsoluteUri) { UseShellExecute=true }); }
        catch(System.ComponentModel.Win32Exception) { }
        catch(InvalidOperationException) { }
    }
}
