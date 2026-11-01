using iText.Kernel.Exceptions;
using iText.Kernel.Pdf;

namespace LillMarkDown;

/// <param name="Markdown">Converted text. Empty when the PDF has no text layer.</param>
/// <param name="PageCount">Pages in the PDF.</param>
/// <param name="PagesWithText">Pages that yielded at least one line of text.</param>
public sealed record ConversionResult(string Markdown, int PageCount, int PagesWithText)
{
    /// <summary>False for scanned / image-only PDFs.</summary>
    public bool HasText => PagesWithText > 0;
}

/// <summary>The PDF needs a password to be opened.</summary>
public sealed class PdfPasswordException(Exception inner) : Exception(inner.Message, inner);

public static class PdfConverter
{
    public static ConversionResult Convert(string pdfPath)
    {
        try
        {
            using var reader = new PdfReader(pdfPath);
            using var doc = new PdfDocument(reader);

            var pages = PdfLayoutExtractor.Extract(doc);
            return new ConversionResult(
                MarkdownBuilder.Build(pages),
                pages.Count,
                pages.Count(p => p.Lines.Count > 0));
        }
        catch (BadPasswordException ex)
        {
            throw new PdfPasswordException(ex);
        }
    }
}
