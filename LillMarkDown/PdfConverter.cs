using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace LillMarkDown;

public static class PdfConverter
{
    public static string Convert(string pdfPath)
    {
        var sb = new StringBuilder();

        using var reader = new PdfReader(pdfPath);
        using var doc   = new PdfDocument(reader);

        int pageCount = doc.GetNumberOfPages();

        for (int p = 1; p <= pageCount; p++)
        {
            if (p > 1)
            {
                sb.AppendLine();
                sb.AppendLine("---");
                sb.AppendLine();
            }

            var strategy = new LocationTextExtractionStrategy();
            string raw = PdfTextExtractor.GetTextFromPage(doc.GetPage(p), strategy);

            if (string.IsNullOrWhiteSpace(raw)) continue;

            AppendMarkdownLines(sb, raw.Split('\n'));
        }

        return sb.ToString().Trim();
    }

    private static void AppendMarkdownLines(StringBuilder sb, string[] rawLines)
    {
        // Pre-pass: rejoin words broken by a PDF line-end hyphen
        // (e.g. "exem-" + "ple" → "exemple"). Only merges when the char
        // before '-' is a letter and the continuation starts with lowercase.
        var lines = MergeHyphenatedLines(rawLines);

        string prev = string.Empty;

        foreach (var raw in lines)
        {
            string line = raw.TrimEnd();
            string trimmed = line.Trim();

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (!string.IsNullOrWhiteSpace(prev))
                    sb.AppendLine();
                prev = string.Empty;
                continue;
            }

            sb.AppendLine(ClassifyLine(trimmed, prev));
            prev = trimmed;
        }
    }

    private static string[] MergeHyphenatedLines(string[] lines)
    {
        var result = new List<string>(lines.Length);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].TrimEnd();
            if (line.Length >= 2
                && line[^1] == '-'
                && char.IsLetter(line[^2])
                && i + 1 < lines.Length)
            {
                string nextTrimmed = lines[i + 1].TrimStart();
                if (nextTrimmed.Length > 0 && char.IsLower(nextTrimmed[0]))
                {
                    result.Add(line[..^1] + nextTrimmed);
                    i++; // next line has been consumed
                    continue;
                }
            }
            result.Add(line);
        }
        return result.ToArray();
    }

    // Heuristic heading detection (LocationTextExtractionStrategy orders text spatially
    // but doesn't expose font size; we use line length + context as proxies)
    private static string ClassifyLine(string line, string prev)
    {
        // All-caps + short → H2; use InvariantCulture and IsLetter so accented-only
        // Catalan headings (e.g. "ÀREA") are also detected.
        if (line.Length <= 80 && line == line.ToUpperInvariant() && line.Any(char.IsLetter))
            return $"## {ToTitleCase(line)}";

        // Short, no trailing sentence punctuation, preceded by blank line → H3
        if (line.Length <= 60
            && !line.EndsWith('.') && !line.EndsWith(',')
            && !line.EndsWith(';') && !line.EndsWith(':')
            && string.IsNullOrWhiteSpace(prev))
            return $"### {line}";

        return EscapeBody(line);
    }

    // Escape leading Markdown block markers so body text isn't mis-rendered as
    // headings, blockquotes, or list items. Inline emphasis is left untouched.
    private static string EscapeBody(string line)
    {
        // ATX heading: # text
        if (line.StartsWith('#'))
            return '\\' + line;

        // Blockquote: > text
        if (line.StartsWith('>'))
            return '\\' + line;

        // Unordered list: - / * / + followed by a space
        if (line.Length >= 2 && line[1] == ' '
            && (line[0] == '-' || line[0] == '*' || line[0] == '+'))
            return '\\' + line;

        // Ordered list: 1. 2. … followed by a space
        if (Regex.IsMatch(line, @"^\d+\. "))
            return Regex.Replace(line, @"^(\d+)\. ", "$1\\. ");

        return line;
    }

    private static string ToTitleCase(string upper)
    {
        if (string.IsNullOrEmpty(upper)) return upper;
        var words = upper.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (w.Length == 0) continue;
            // Words with no vowels are almost certainly acronyms (PDF, HTML, XML,
            // HTTPS…) — keep them unchanged so they aren't mangled to "Pdf", "Html".
            words[i] = w.Any(IsVowel)
                ? char.ToUpper(w[0]) + w[1..].ToLower()
                : w;
        }
        return string.Join(' ', words);
    }

    // Catalan vowels including accented forms.
    private static bool IsVowel(char c) =>
        "aeiouàèéíïòóúüAEIOUÀÈÉÍÏÒÓÚÜ".Contains(c);
}
