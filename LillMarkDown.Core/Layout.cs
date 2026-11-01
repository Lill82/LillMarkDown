namespace LillMarkDown;

/// <summary>
/// One visual line of text on a page. Coordinates are PDF points relative to the
/// page's crop box, with Y growing upwards (so a higher <see cref="Baseline"/> is
/// nearer the top of the page).
/// </summary>
/// <param name="Text">Line text, whitespace-normalised.</param>
/// <param name="FontSize">Dominant effective font size in points (weighted by character count).</param>
/// <param name="Bold">True when (almost) every character of the line uses a bold font.</param>
/// <param name="Left">X of the first character.</param>
/// <param name="Right">X where the last character ends.</param>
/// <param name="Baseline">Y of the text baseline.</param>
public sealed record TextLine(string Text, float FontSize, bool Bold, float Left, float Right, float Baseline);

/// <summary>A page's lines in reading order (top to bottom).</summary>
public sealed record PageLayout(int Number, float Width, float Height, IReadOnlyList<TextLine> Lines);
