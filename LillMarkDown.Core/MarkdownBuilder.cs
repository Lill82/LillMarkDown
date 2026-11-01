using System.Text;
using System.Text.RegularExpressions;

namespace LillMarkDown;

/// <summary>
/// Turns positioned text lines into Markdown. Pure function of its input, so the
/// rules can be unit-tested without a PDF.
///
/// Pipeline: drop running headers/footers → measure the document (body size,
/// heading sizes, line spacing) → group each page's lines into blocks
/// (paragraphs, headings, list items) → render each block.
/// </summary>
public static class MarkdownBuilder
{
    public const string PageSeparator = "---";

    public static string Build(IReadOnlyList<PageLayout> pages)
    {
        var cleaned = RemoveRunningHeadersAndFooters(pages, DocumentStats.From(pages).BodySize);
        var stats = DocumentStats.From(cleaned);

        var sb = new StringBuilder();
        foreach (var page in cleaned)
        {
            var blocks = GroupBlocks(page, stats);
            if (blocks.Count == 0) continue;

            if (sb.Length > 0)
                sb.Append("\n\n").Append(PageSeparator).Append("\n\n");

            for (int i = 0; i < blocks.Count; i++)
            {
                if (i > 0)
                    // Consecutive list items stay together as one tight list.
                    sb.Append(blocks[i].IsListItem && blocks[i - 1].IsListItem ? "\n" : "\n\n");
                sb.Append(Render(blocks[i], stats));
            }
        }
        return sb.ToString();
    }

    // ── Running headers, footers and page numbers ────────────────────────────

    private const float MarginZone = 0.08f; // top/bottom 8 % of the page

    internal static IReadOnlyList<PageLayout> RemoveRunningHeadersAndFooters(IReadOnlyList<PageLayout> pages, float bodySize)
    {
        // Heading-size text is never a running header ("Capítol 3" at the top of a page).
        bool InMargin(TextLine l, PageLayout p) =>
            (l.Baseline >= p.Height * (1 - MarginZone) || l.Baseline <= p.Height * MarginZone)
            && l.FontSize < bodySize * DocumentStats.HeadingRatio;

        static string Key(string text) => Digits.Replace(text.ToLowerInvariant(), "#");

        // How many pages each margin text (digits normalised) appears on.
        var counts = new Dictionary<string, int>();
        foreach (var page in pages)
            foreach (var key in page.Lines.Where(l => InMargin(l, page)).Select(l => Key(l.Text)).Distinct())
                counts[key] = counts.GetValueOrDefault(key) + 1;

        int threshold = pages.Count >= 3
            ? Math.Max(2, (int)Math.Ceiling(pages.Count * 0.4))
            : int.MaxValue;

        return pages
            .Select(page => page with
            {
                Lines = page.Lines
                    .Where(l => !(InMargin(l, page)
                                  && (PageNumber.IsMatch(l.Text) || counts[Key(l.Text)] >= threshold)))
                    .ToList()
            })
            .ToList();
    }

    private static readonly Regex Digits = new(@"\d+", RegexOptions.Compiled);

    // "3", "- 3 -", "Pàgina 3", "Pàg. 3 de 10", "Page 3 of 10", "3 / 10"
    private static readonly Regex PageNumber = new(
        @"^[\s\-–—]*(?:(?:p[àaá]g(?:ina)?|page|p)\.?\s*)?\d{1,4}(?:\s*(?:/|de|of)\s*\d{1,4})?[\s\-–—]*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Document statistics ─────────────────────────────────────────────────

    internal sealed class DocumentStats
    {
        public float BodySize { get; private init; }

        /// <summary>Distinct heading font sizes, largest first (at most 3 → #, ##, ###).</summary>
        public IReadOnlyList<float> HeadingSizes { get; private init; } = [];

        /// <summary>Level for bold or all-caps headings that use the body font size.</summary>
        public int MinorHeadingLevel => Math.Min(Math.Max(HeadingSizes.Count, 1) + 1, 4);

        private Dictionary<float, float> _leading = [];

        public float Leading(float size) => _leading.TryGetValue(size, out var l) ? l : size * 1.2f;

        public int? HeadingLevelForSize(float size)
        {
            if (HeadingSizes.Count == 0 || size < BodySize * HeadingRatio) return null;
            for (int i = 0; i < HeadingSizes.Count; i++)
                if (size >= HeadingSizes[i] - 0.25f) return i + 1;
            return HeadingSizes.Count; // bigger than body but smaller than the tracked sizes
        }

        public const float HeadingRatio = 1.15f;

        public static DocumentStats From(IReadOnlyList<PageLayout> pages)
        {
            var lines = pages.SelectMany(p => p.Lines).ToList();
            if (lines.Count == 0) return new DocumentStats();

            // Body size = the size carrying the most characters.
            float body = lines
                .GroupBy(l => l.FontSize)
                .MaxBy(g => g.Sum(l => l.Text.Length))!
                .Key;

            var headingSizes = lines
                .Where(l => l.FontSize >= body * HeadingRatio && l.Text.Length <= 200)
                .Select(l => l.FontSize)
                .Distinct()
                .OrderDescending()
                .Take(3)
                .ToList();

            // Typical baseline-to-baseline distance for each font size.
            var gaps = new Dictionary<float, List<float>>();
            foreach (var page in pages)
                for (int i = 1; i < page.Lines.Count; i++)
                {
                    var a = page.Lines[i - 1];
                    var b = page.Lines[i];
                    if (a.FontSize != b.FontSize) continue;
                    float gap = a.Baseline - b.Baseline;
                    if (gap <= 0 || gap > a.FontSize * 2.5f) continue;
                    if (!gaps.TryGetValue(a.FontSize, out var list)) gaps[a.FontSize] = list = [];
                    list.Add(gap);
                }

            return new DocumentStats
            {
                BodySize = body,
                HeadingSizes = headingSizes,
                _leading = gaps.ToDictionary(kv => kv.Key, kv => Median(kv.Value)),
            };
        }

        private static float Median(List<float> values)
        {
            values.Sort();
            return values[values.Count / 2];
        }
    }

    // ── Blocks ──────────────────────────────────────────────────────────────

    internal sealed class Block(TextLine first, bool isListItem)
    {
        public List<TextLine> Lines { get; } = [first];
        public bool IsListItem { get; } = isListItem;
        public float FontSize => Lines[0].FontSize;
        public bool Bold => Lines[0].Bold;
    }

    internal static List<Block> GroupBlocks(PageLayout page, DocumentStats stats)
    {
        var blocks = new List<Block>();
        if (page.Lines.Count == 0) return blocks;

        // Right edge of the text column, used to spot a paragraph's short last line.
        var bodyLines = page.Lines.Where(l => Math.Abs(l.FontSize - stats.BodySize) < 0.75f).ToList();
        float columnLeft = bodyLines.Count > 0 ? bodyLines.Min(l => l.Left) : page.Lines.Min(l => l.Left);
        float columnRight = bodyLines.Count > 0 ? bodyLines.Max(l => l.Right) : page.Lines.Max(l => l.Right);

        Block? current = null;
        TextLine? prev = null;
        foreach (var line in page.Lines)
        {
            bool listStart = ListItem.IsMatch(line.Text);
            if (current is null || prev is null
                || StartsNewBlock(current, prev, line, listStart, stats, columnLeft, columnRight))
            {
                current = new Block(line, listStart);
                blocks.Add(current);
            }
            else
            {
                current.Lines.Add(line);
            }
            prev = line;
        }
        return blocks;
    }

    private static bool StartsNewBlock(Block block, TextLine prev, TextLine line, bool listStart,
                                       DocumentStats stats, float columnLeft, float columnRight)
    {
        if (listStart) return true;

        // Change of style: heading ↔ body, footnotes, bold label …
        if (Math.Abs(line.FontSize - prev.FontSize) > 0.75f) return true;
        if (line.Bold != prev.Bold) return true;
        if (IsAllCapsHeading(prev.Text) != IsAllCapsHeading(line.Text)) return true;

        // Vertical gap bigger than normal line spacing, or a jump back up (new column).
        float gap = prev.Baseline - line.Baseline;
        if (gap <= 0 || gap > stats.Leading(prev.FontSize) * 1.4f) return true;

        float em = prev.FontSize;

        // A list item continues while lines stay indented past the bullet.
        if (block.IsListItem)
            return line.Left < block.Lines[0].Left + em * 0.5f;

        // A line well short of either margin ends its paragraph (last line of a
        // paragraph, or one of several centred lines) when the next line starts
        // like a new sentence. Lowercase means it is still the same sentence.
        // Not for heading-style lines: a wrapped heading must stay in one piece.
        bool headingStyle = prev.Bold
                            || stats.HeadingLevelForSize(prev.FontSize) is not null
                            || IsAllCapsHeading(prev.Text);
        float slack = Math.Max(em * 4f, (columnRight - columnLeft) * 0.15f);
        bool prevShort = prev.Right < columnRight - slack || prev.Left > columnLeft + slack;
        if (!headingStyle && prevShort && !char.IsLower(line.Text[0])) return true;

        // First-line indent of the next paragraph.
        if (SentenceEnd.IsMatch(prev.Text) && line.Left > prev.Left + em * 0.8f) return true;

        return false;
    }

    private static readonly Regex SentenceEnd = new(@"[.!?:][""”»’)]?$", RegexOptions.Compiled);

    // ── Rendering ───────────────────────────────────────────────────────────

    // Bullets need a space after ASCII markers (so "-5 %" or "*nota" are not lists);
    // typographic bullets don't. Numbered: "1." "1)" ; lettered: "a)".
    internal static readonly Regex ListItem = new(
        @"^(?:(?<bullet>[•◦▪▫●○■□‣⁃∙·➢➤►▶✓✔])\s*|(?<bullet>[-–—*+])\s+|(?<num>\d{1,2})[.)]\s+|(?<alpha>[a-zA-Z])\)\s+)(?<rest>\S.*)$",
        RegexOptions.Compiled);

    private static readonly Regex TrailingPunctuation = new(@"[.,;:]$", RegexOptions.Compiled);

    internal static string Render(Block block, DocumentStats stats)
    {
        string text = JoinLines(block.Lines);

        if (block.IsListItem)
            return RenderListItem(text);

        if (stats.HeadingLevelForSize(block.FontSize) is int level && text.Length <= 200)
            return Heading(level, text);

        if (block.Bold && text.Length <= 120 && !TrailingPunctuation.IsMatch(text))
            return Heading(stats.MinorHeadingLevel, text);

        if (block.Lines.Count == 1 && IsAllCapsHeading(text))
            return Heading(stats.MinorHeadingLevel, text);

        return EscapeBody(text);
    }

    private static string Heading(int level, string text)
    {
        if (IsAllCaps(text)) text = ToTitleCase(text);
        return new string('#', level) + " " + text;
    }

    private static string RenderListItem(string text)
    {
        var m = ListItem.Match(text);
        if (!m.Success) return EscapeBody(text);

        string rest = EscapeBody(m.Groups["rest"].Value);
        if (m.Groups["num"].Success) return $"{m.Groups["num"].Value}. {rest}";
        if (m.Groups["alpha"].Success) return $"- {m.Groups["alpha"].Value}) {rest}";
        return $"- {rest}";
    }

    /// <summary>Joins wrapped lines, re-merging words split by an end-of-line hyphen.</summary>
    internal static string JoinLines(IReadOnlyList<TextLine> lines)
    {
        var sb = new StringBuilder(lines[0].Text);
        for (int i = 1; i < lines.Count; i++)
        {
            string next = lines[i].Text;
            if (sb[^1] == '­') // soft hyphen
            {
                sb.Length--;
                sb.Append(next);
            }
            else if (sb.Length >= 2 && sb[^1] == '-' && char.IsLetter(sb[^2]) && char.IsLower(next[0]))
            {
                // "exem-" + "ple" → "exemple"
                sb.Length--;
                sb.Append(next);
            }
            else
            {
                sb.Append(' ').Append(next);
            }
        }
        return sb.ToString().Replace("­", string.Empty);
    }

    // ── Escaping ────────────────────────────────────────────────────────────

    /// <summary>
    /// Escapes leading characters that Markdown would read as block syntax
    /// (heading, quote, list, table, code fence, HTML, thematic break).
    /// Inline emphasis is left untouched.
    /// </summary>
    internal static string EscapeBody(string text)
    {
        if (text.Length == 0) return text;

        if (OrderedListStart.Match(text) is { Success: true } m)
            return m.Groups[1].Value + "\\" + text[m.Groups[1].Length..];

        if (text[0] is '#' or '>' or '|' or '<'
            || text.StartsWith("```") || text.StartsWith("~~~")
            || ThematicBreak.IsMatch(text)
            || (text.Length >= 2 && text[0] is '-' or '*' or '+' && char.IsWhiteSpace(text[1])))
            return "\\" + text;

        return text;
    }

    private static readonly Regex OrderedListStart = new(@"^(\d+)[.)](?:\s|$)", RegexOptions.Compiled);
    private static readonly Regex ThematicBreak = new(@"^(?:[-*_=]\s*){3,}$", RegexOptions.Compiled);

    // ── Case helpers ────────────────────────────────────────────────────────

    private static bool IsAllCaps(string text) =>
        text.Any(char.IsLetter) && text == text.ToUpperInvariant();

    /// <summary>Short all-caps line with at least 4 letters, no sentence punctuation.</summary>
    internal static bool IsAllCapsHeading(string text) =>
        text.Length <= 80
        && text.Count(char.IsLetter) >= 4
        && IsAllCaps(text)
        && !TrailingPunctuation.IsMatch(text);

    internal static string ToTitleCase(string upper)
    {
        var words = upper.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            string w = words[i];
            // Words with no vowels are almost certainly acronyms (PDF, HTML, XML…);
            // keep them so they aren't mangled to "Pdf", "Html".
            if (w.Any(IsVowel) && !RomanNumeral.IsMatch(w))
                words[i] = char.ToUpperInvariant(w[0]) + w[1..].ToLowerInvariant();
        }
        return string.Join(' ', words);
    }

    // "IV." "XII)" — a valid Roman numeral, optionally followed by . or )
    private static readonly Regex RomanNumeral = new(
        @"^(?=[MDCLXVI])M{0,3}(?:CM|CD|D?C{0,3})(?:XC|XL|L?X{0,3})(?:IX|IV|V?I{0,3})[.)]?$",
        RegexOptions.Compiled);

    private static bool IsVowel(char c) => "aeiouàèéíïòóúüAEIOUÀÈÉÍÏÒÓÚÜ".Contains(c);
}
