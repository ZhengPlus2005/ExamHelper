using System.Text;
namespace ExamHelper.Core.Ai;
public enum AnswerSegmentKind { Plain,Code }
public sealed record AnswerSegment(AnswerSegmentKind Kind,string Text,string Language = "");
public static class CodeBlockParser
{
    public static IReadOnlyList<AnswerSegment> Parse(string accumulatedText,bool isFinal)
    {
        var result = new List<AnswerSegment>(); var text = new StringBuilder(); bool code = false; string language = ""; int fence = 3;
        void Flush() { if(text.Length > 0 || code) result.Add(new(code ? AnswerSegmentKind.Code : AnswerSegmentKind.Plain,text.ToString(),code ? language : "")); text.Clear(); }
        int offset = 0;
        while(offset < accumulatedText.Length)
        {
            var end = accumulatedText.IndexOf('\n',offset); var hasNewline = end >= 0;
            if(!hasNewline) end = accumulatedText.Length - 1;
            var line = accumulatedText[offset..(end + 1)]; var body = line.TrimEnd('\r','\n');
            var ticks = body.TakeWhile(c => c == '`').Count();
            if(!code && ticks >= 3 && (hasNewline || isFinal))
            {
                Flush(); code = true; fence = ticks; language = body[ticks..].Trim();
            }
            else if(code && ticks >= fence && body[ticks..].Trim().Length == 0)
            {
                Flush(); code = false; language = "";
            }
            else text.Append(line);
            offset = end + 1;
        }
        Flush(); return result;
    }
}