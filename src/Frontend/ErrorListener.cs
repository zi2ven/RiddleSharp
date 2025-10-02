using System.Text;
using Antlr4.Runtime;

namespace RiddleSharp.Frontend;

public sealed class ErrorListener(string source, string? fileName = null, int tabSize = 4)
    : BaseErrorListener, IAntlrErrorListener<int>
{
    public readonly List<string> Messages = [];
    private readonly string _fileName = string.IsNullOrEmpty(fileName) ? "(input)" : fileName;
    private int _lastTokIndex = int.MinValue;

    public override void SyntaxError(TextWriter output,IRecognizer recognizer, IToken offendingSymbol,
        int line, int charPositionInLine, string msg, RecognitionException? e)
    {
        if (offendingSymbol != null && offendingSymbol.TokenIndex == _lastTokIndex) return;
        _lastTokIndex = offendingSymbol?.TokenIndex ?? int.MinValue;
        
        var vocab = (recognizer as Parser)?.Vocabulary;
        var expected = e?.GetExpectedTokens()?.ToString(vocab) ?? "";
        var reason = ClassifyReason(e!, msg);

        BuildOne(line, charPositionInLine,
            tokenText: offendingSymbol!.Text,
            tokenLen: offendingSymbol.Type == TokenConstants.EOF
                ? 1
                : Math.Max(1, offendingSymbol.StopIndex - offendingSymbol.StartIndex + 1),
            tail: expected.Length > 0
                ? $"SyntaxError: {reason}, expected {Shorten(expected, 6)}"
                : $"SyntaxError: {reason}");
    }
    
    public void SyntaxError(TextWriter output,IRecognizer recognizer, int offendingChar,
        int line, int charPositionInLine, string msg, RecognitionException e)
    {
        BuildOne(line, charPositionInLine, tokenText: null, tokenLen: 1,
            tail: $"SyntaxError: {msg}");
    }
    
    
    private void BuildOne(int line1, int col0, string? tokenText, int tokenLen, string tail)
    {
        var (lineTextRaw, exists) = GetLine(line1);
        var lineText = ExpandTabs(lineTextRaw, tabSize).TrimEnd('\r', '\n');
        
        var startCol = Math.Min(col0, lineText.Length);
        var width = Math.Max(1, tokenLen);
        // 宽度不过行尾
        width = Math.Min(width, Math.Max(1, lineText.Length - startCol));

        var caretLine = new string(' ', 4 + startCol) + (width == 1 ? "^" : new string('^', width));

        var sb = new StringBuilder();
        sb.AppendLine($"  File \"{_fileName}\", line {line1}");
        sb.AppendLine($"    {lineText}");
        sb.AppendLine(caretLine);
        sb.Append(tail);

        if (!exists)
            sb.Append(" (note: source line not found)");

        Messages.Add(sb.ToString());
    }

    private (string, bool) GetLine(int line1Based)
    {
        if (line1Based <= 0) return ("", false);
        int cur = 1, i = 0, start = 0;
        while (i < source.Length && cur < line1Based)
        {
            if (source[i] == '\n') { cur++; start = i + 1; }
            i++;
        }
        if (cur != line1Based) return ("", false);
        var end = start;
        while (end < source.Length && source[end] != '\n') end++;
        return (source[start..Math.Min(end, source.Length)], true);
    }

    private static string ExpandTabs(string s, int tabSize)
    {
        if (tabSize <= 0 || string.IsNullOrEmpty(s) || s.IndexOf('\t') < 0) return s;
        var col = 0;
        var sb = new StringBuilder(s.Length + 8);
        foreach (var ch in s)
        {
            if (ch == '\t')
            {
                var spaces = tabSize - (col % tabSize);
                sb.Append(' ', spaces);
                col += spaces;
            }
            else
            {
                sb.Append(ch);
                col++;
            }
        }
        return sb.ToString();
    }

    private static string Shorten(string expectedSet, int maxItems)
    {
        // expectedSet  "{'+', ID, INT, ...}"
        var trimmed = expectedSet.Trim();
        if (!(trimmed.StartsWith(") && trimmed.EndsWith("))) return trimmed;
        var items = trimmed.Substring(1, trimmed.Length - 2)
                           .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length <= maxItems) return trimmed;
        return "{" + string.Join(", ", items.Take(maxItems)) + ", …}";
    }

    private static string ClassifyReason(RecognitionException e, string fallback)
        => e switch
        {
            NoViableAltException => "invalid syntax",
            InputMismatchException => "invalid syntax",
            FailedPredicateException => "invalid syntax (failed predicate)",
            _ => string.IsNullOrWhiteSpace(fallback) ? "invalid syntax" : fallback
        };
}
