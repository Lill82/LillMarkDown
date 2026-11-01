using System.Text;
using System.Text.RegularExpressions;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Data;
using iText.Kernel.Pdf.Canvas.Parser.Listener;

namespace LillMarkDown;

/// <summary>
/// Reads every text-render event on a page (with its font, effective size and
/// position) and groups the fragments into visual lines. Unlike
/// <c>LocationTextExtractionStrategy</c>, this keeps the font metrics that
/// <see cref="MarkdownBuilder"/> needs to detect headings and paragraphs.
/// </summary>
public static class PdfLayoutExtractor
{
    public static IReadOnlyList<PageLayout> Extract(PdfDocument doc)
    {
        var pages = new List<PageLayout>(doc.GetNumberOfPages());
        for (int p = 1; p <= doc.GetNumberOfPages(); p++)
        {
            var page = doc.GetPage(p);
            Rectangle box = page.GetCropBox();

            var collector = new ChunkCollector(box.GetLeft(), box.GetBottom());
            new PdfCanvasProcessor(collector).ProcessPageContent(page);

            pages.Add(new PageLayout(p, box.GetWidth(), box.GetHeight(), BuildLines(collector.Chunks)));
        }
        return pages;
    }

    // ── Fragments ────────────────────────────────────────────────────────────

    private sealed record Chunk(string Text, float X, float EndX, float Y, float Size, bool Bold, float SpaceWidth);

    private sealed class ChunkCollector(float originX, float originY) : IEventListener
    {
        public List<Chunk> Chunks { get; } = [];

        public void EventOccurred(IEventData data, EventType type)
        {
            if (type != EventType.RENDER_TEXT || data is not TextRenderInfo info) return;

            string text = info.GetText();
            if (string.IsNullOrEmpty(text)) return;

            Vector start = info.GetBaseline().GetStartPoint();
            Vector end   = info.GetBaseline().GetEndPoint();
            float dx = end.Get(Vector.I1) - start.Get(Vector.I1);
            float dy = end.Get(Vector.I2) - start.Get(Vector.I2);

            // Skip rotated / vertical text (margin labels, watermarks); it would
            // otherwise be interleaved with the body lines.
            if (Math.Abs(dy) > Math.Abs(dx) * 0.1f && Math.Abs(dy) > 0.5f) return;

            float size = EffectiveFontSize(info);
            if (size < 1f) return; // invisible / degenerate text

            Chunks.Add(new Chunk(
                text,
                start.Get(Vector.I1) - originX,
                end.Get(Vector.I1) - originX,
                start.Get(Vector.I2) - originY,
                size,
                IsBold(info),
                info.GetSingleSpaceWidth()));
        }

        public ICollection<EventType> GetSupportedEvents() => [EventType.RENDER_TEXT];
    }

    // Tf size scaled by the text and transformation matrices = the size the reader sees.
    private static float EffectiveFontSize(TextRenderInfo info)
    {
        Matrix m = info.GetTextMatrix().Multiply(info.GetGraphicsState().GetCtm());
        float sx = m.Get(Matrix.I21), sy = m.Get(Matrix.I22);
        return info.GetFontSize() * MathF.Sqrt(sx * sx + sy * sy);
    }

    private static bool IsBold(TextRenderInfo info)
    {
        // Text render mode 2 = fill + stroke, a common "fake bold".
        if (info.GetTextRenderMode() == 2) return true;

        var names = info.GetFont()?.GetFontProgram()?.GetFontNames();
        if (names is null) return false;
        if (names.GetFontWeight() >= 600) return true;

        string name = names.GetFontName() ?? string.Empty;
        return BoldName.IsMatch(name);
    }

    private static readonly Regex BoldName =
        new(@"bold|black|heavy|semibold|demibold|demi\b|\bdemi", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // ── Lines ────────────────────────────────────────────────────────────────

    private static List<TextLine> BuildLines(List<Chunk> chunks)
    {
        // Top-to-bottom, then left-to-right.
        var ordered = chunks.OrderByDescending(c => c.Y).ThenBy(c => c.X).ToList();
        var groups = new List<List<Chunk>>();

        foreach (var c in ordered)
        {
            var current = groups.Count > 0 ? groups[^1] : null;
            if (current is not null)
            {
                float lineY = current[0].Y;
                float tolerance = 0.45f * Math.Max(c.Size, current[0].Size);
                if (Math.Abs(c.Y - lineY) <= tolerance)
                {
                    current.Add(c);
                    continue;
                }
            }
            groups.Add([c]);
        }

        var lines = new List<TextLine>(groups.Count);
        foreach (var g in groups)
        {
            var line = ToLine(g);
            if (line is not null) lines.Add(line);
        }
        return lines;
    }

    private static TextLine? ToLine(List<Chunk> group)
    {
        group.Sort((a, b) => a.X.CompareTo(b.X));

        var sb = new StringBuilder();
        Chunk? prev = null;
        var sizeWeights = new Dictionary<float, int>();
        int boldChars = 0, totalChars = 0;

        foreach (var c in group)
        {
            if (prev is not null)
            {
                // Same text drawn twice at (nearly) the same spot = fake bold / shadow.
                if (c.Text == prev.Text && Math.Abs(c.X - prev.X) < 1f) continue;

                float gap = c.X - prev.EndX;
                float space = c.SpaceWidth > 0 ? c.SpaceWidth : c.Size * 0.25f;
                bool hasSpace = sb.Length > 0 && char.IsWhiteSpace(sb[^1]) || char.IsWhiteSpace(c.Text[0]);
                if (!hasSpace && gap > space * 0.4f)
                    sb.Append(' ');
            }

            sb.Append(c.Text);
            prev = c;

            int visible = c.Text.Count(ch => !char.IsWhiteSpace(ch));
            if (visible == 0) continue;
            float rounded = MathF.Round(c.Size * 2f) / 2f;
            sizeWeights[rounded] = sizeWeights.GetValueOrDefault(rounded) + visible;
            totalChars += visible;
            if (c.Bold) boldChars += visible;
        }

        string text = Whitespace.Replace(sb.ToString(), " ").Trim();
        if (text.Length == 0 || totalChars == 0) return null;

        float size = sizeWeights.MaxBy(kv => kv.Value).Key;
        return new TextLine(
            text,
            size,
            Bold: boldChars >= totalChars * 0.9,
            Left: group.Min(c => c.X),
            Right: group.Max(c => c.EndX),
            Baseline: group.Average(c => c.Y));
    }

    private static readonly Regex Whitespace = new(@"[\s ]+", RegexOptions.Compiled);
}
