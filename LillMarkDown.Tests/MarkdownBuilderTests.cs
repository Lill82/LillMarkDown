namespace LillMarkDown.Tests;

public class MarkdownBuilderTests
{
    // A4 page; body column from x=72 to x=523; body text 10 pt with 12 pt leading.
    private const float Width = 595, Height = 842, ColLeft = 72, ColRight = 523;

    private static TextLine Line(string text, float y, float size = 10, bool bold = false,
                                 float left = ColLeft, float? right = null) =>
        new(text, size, bold, left, right ?? ColRight, y);

    /// <summary>A body line that does not reach the right margin.</summary>
    private static TextLine Short(string text, float y, float size = 10, bool bold = false, float left = ColLeft) =>
        Line(text, y, size, bold, left, left + text.Length * size * 0.5f);

    private static PageLayout Page(int n, params TextLine[] lines) => new(n, Width, Height, lines);

    private static string Build(params PageLayout[] pages) => MarkdownBuilder.Build(pages);

    [Fact]
    public void Font_sizes_map_to_heading_levels()
    {
        var md = Build(Page(1,
            Short("Informe", 780, size: 24),
            Line("Primera línia del paràgraf que arriba fins al marge dret de la", 750),
            Short("columna.", 738),
            Short("Secció", 710, size: 16),
            Short("Text de la secció.", 690)));

        Assert.Equal(
            "# Informe\n\n" +
            "Primera línia del paràgraf que arriba fins al marge dret de la columna.\n\n" +
            "## Secció\n\n" +
            "Text de la secció.",
            md);
    }

    [Fact]
    public void Wrapped_lines_join_into_one_paragraph_and_gaps_split_them()
    {
        var md = Build(Page(1,
            Line("Una línia llarga que continua", 700),
            Short("a la següent.", 688),
            Short("Un altre paràgraf després d'un espai.", 664)));

        Assert.Equal(
            "Una línia llarga que continua a la següent.\n\n" +
            "Un altre paràgraf després d'un espai.",
            md);
    }

    [Fact]
    public void Short_last_line_ending_a_sentence_closes_the_paragraph()
    {
        var md = Build(Page(1,
            Line("Primer paràgraf amb text suficient per omplir la línia sencera i", 700),
            Short("acabar aquí.", 688),
            Line("Segon paràgraf que comença just a la línia següent sense cap espai", 676),
            Short("extra.", 664)));

        Assert.Equal(
            "Primer paràgraf amb text suficient per omplir la línia sencera i acabar aquí.\n\n" +
            "Segon paràgraf que comença just a la línia següent sense cap espai extra.",
            md);
    }

    [Fact]
    public void Centred_lines_without_bullets_stay_separate()
    {
        var md = Build(Page(1,
            Line("Primer titular centrat", 700, left: 150, right: 445),
            Line("Segon titular centrat", 688, left: 152, right: 443),
            Line("Paràgraf normal que ocupa tota l'amplada de la columna de text,", 664),
            Short("i acaba aquí.", 652)));

        Assert.Equal(
            "Primer titular centrat\n\nSegon titular centrat\n\n" +
            "Paràgraf normal que ocupa tota l'amplada de la columna de text, i acaba aquí.",
            md);
    }

    [Fact]
    public void Short_line_followed_by_lowercase_continues_the_sentence()
    {
        var md = Build(Page(1,
            Line("Una frase que es talla", 700, left: 150, right: 445),
            Line("a mitja línia.", 688, left: 200, right: 395)));

        Assert.Equal("Una frase que es talla a mitja línia.", md);
    }

    [Fact]
    public void Hyphenated_line_ends_are_rejoined()
    {
        var md = Build(Page(1,
            Line("Aquest és un exem-", 700),
            Short("ple de guionatge.", 688)));

        Assert.Equal("Aquest és un exemple de guionatge.", md);
    }

    [Fact]
    public void Hyphen_before_capital_is_kept()
    {
        var md = Build(Page(1,
            Line("Nord-", 700),
            Short("Amèrica i Europa.", 688)));

        Assert.Equal("Nord- Amèrica i Europa.", md);
    }

    [Fact]
    public void Bullets_and_numbers_become_a_tight_list()
    {
        var md = Build(Page(1,
            Short("• Primer element", 700),
            Line("• Segon element, que és prou llarg per ocupar tota la línia i", 688),
            Short("continua indentat.", 676, left: ColLeft + 12),
            Short("1) Pas numerat", 664),
            Short("a) Opció amb lletra", 652)));

        Assert.Equal(
            "- Primer element\n" +
            "- Segon element, que és prou llarg per ocupar tota la línia i continua indentat.\n" +
            "1. Pas numerat\n" +
            "- a) Opció amb lletra",
            md);
    }

    [Fact]
    public void Text_back_at_the_bullet_column_ends_the_list()
    {
        var md = Build(Page(1,
            Short("- Element", 700),
            Short("Paràgraf normal.", 688)));

        Assert.Equal("- Element\n\nParàgraf normal.", md);
    }

    [Fact]
    public void Running_headers_footers_and_page_numbers_are_removed()
    {
        PageLayout P(int n) => Page(n,
            Short("Informe anual 2025", 815),
            Short($"Contingut de la pàgina {n}.", 700),
            Short($"Pàgina {n} de 3", 30));

        var md = Build(P(1), P(2), P(3));

        Assert.Equal(
            "Contingut de la pàgina 1.\n\n---\n\n" +
            "Contingut de la pàgina 2.\n\n---\n\n" +
            "Contingut de la pàgina 3.",
            md);
    }

    [Fact]
    public void Margin_text_that_does_not_repeat_is_kept()
    {
        var md = Build(
            Page(1, Short("Nota única al peu.", 40), Short("Cos.", 700)),
            Page(2, Short("Cos.", 700)),
            Page(3, Short("Cos.", 700)));

        Assert.Contains("Nota única al peu.", md);
    }

    [Fact]
    public void Heading_at_the_top_of_every_page_is_kept()
    {
        PageLayout P(int n) => Page(n,
            Short($"Capítol {n}", 800, size: 18),
            Short("El cos del capítol té més text que el títol.", 700));

        var md = Build(P(1), P(2), P(3));

        Assert.Contains("# Capítol 1", md);
        Assert.Contains("# Capítol 3", md);
    }

    [Fact]
    public void Bold_body_size_line_is_a_minor_heading()
    {
        var md = Build(Page(1,
            Short("Objectius", 700, bold: true),
            Short("Text normal.", 686)));

        Assert.Equal("## Objectius\n\nText normal.", md);
    }

    [Fact]
    public void Wrapped_centred_heading_stays_one_heading()
    {
        var md = Build(Page(1,
            Line("Informe del quart trimestre", 760, size: 16, bold: true, left: 180, right: 415),
            Line("i de l'exercici 2025", 742, size: 16, bold: true, left: 220, right: 375),
            Line("Cos del document amb prou text per definir la columna sencera de", 700),
            Short("text.", 688)));

        Assert.StartsWith("# Informe del quart trimestre i de l'exercici 2025\n\n", md);
    }

    [Fact]
    public void Bold_line_with_trailing_colon_is_not_a_heading()
    {
        var md = Build(Page(1, Short("Nota:", 700, bold: true)));

        Assert.Equal("Nota:", md);
    }

    [Fact]
    public void All_caps_line_is_a_heading_in_title_case()
    {
        var md = Build(Page(1,
            Short("INTRODUCCIÓ AL PDF", 700),
            Short("Text del cos.", 688)));

        Assert.Equal("## Introducció Al PDF\n\nText del cos.", md);
    }

    [Fact]
    public void Empty_document_gives_empty_markdown()
    {
        Assert.Equal(string.Empty, Build(Page(1), Page(2)));
    }

    [Theory]
    [InlineData("# no és títol", "\\# no és títol")]
    [InlineData("> no és cita", "\\> no és cita")]
    [InlineData("- no és llista", "\\- no és llista")]
    [InlineData("1. no és llista", "1\\. no és llista")]
    [InlineData("2) tampoc", "2\\) tampoc")]
    [InlineData("---", "\\---")]
    [InlineData("| a | b |", "\\| a | b |")]
    [InlineData("<div>", "\\<div>")]
    [InlineData("```", "\\```")]
    [InlineData("Text normal", "Text normal")]
    [InlineData("-5 % de descompte", "-5 % de descompte")]
    public void Body_text_that_looks_like_markdown_is_escaped(string input, string expected)
    {
        Assert.Equal(expected, MarkdownBuilder.EscapeBody(input));
    }

    [Theory]
    [InlineData("L'ÀREA DE PDF", "L'àrea De PDF")]
    [InlineData("HTML I XML", "HTML I XML")]
    [InlineData("IV. SOL·LICITUDS", "IV. Sol·licituds")]
    [InlineData("CIVIL", "Civil")]
    public void Title_case_keeps_acronyms(string input, string expected)
    {
        Assert.Equal(expected, MarkdownBuilder.ToTitleCase(input));
    }
}
