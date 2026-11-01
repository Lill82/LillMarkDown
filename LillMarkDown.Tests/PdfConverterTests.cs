using System.Text;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;

namespace LillMarkDown.Tests;

/// <summary>End-to-end: generate a real PDF with iText, convert it, check the Markdown.</summary>
public sealed class PdfConverterTests : IDisposable
{
    private readonly List<string> _files = [];

    public void Dispose()
    {
        foreach (var f in _files) File.Delete(f);
    }

    private string NewPdf(Action<Document> write, WriterProperties? props = null)
    {
        string path = Path.Combine(Path.GetTempPath(), $"lillmd-{Guid.NewGuid():N}.pdf");
        _files.Add(path);
        using (var writer = new PdfWriter(path, props ?? new WriterProperties()))
        using (var pdf = new PdfDocument(writer))
        using (var doc = new Document(pdf))
        {
            doc.SetMargins(72, 72, 72, 72); // typical 1-inch margins
            write(doc);
        }
        return path;
    }

    private const string LongText =
        "Aquest paràgraf és prou llarg perquè el motor de maquetació l'hagi de partir en diverses " +
        "línies, i el convertidor les ha de tornar a unir en un sol paràgraf de Markdown sense perdre " +
        "cap paraula ni afegir salts de línia on no n'hi ha.";

    [Fact]
    public void Headings_by_size_and_bold_and_wrapped_paragraphs()
    {
        var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var bold = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

        string pdf = NewPdf(doc =>
        {
            doc.Add(new Paragraph("Títol del document").SetFont(bold).SetFontSize(24));
            doc.Add(new Paragraph(LongText).SetFont(regular).SetFontSize(11));
            doc.Add(new Paragraph("Secció primera").SetFont(bold).SetFontSize(16));
            doc.Add(new Paragraph("Apartat en negreta").SetFont(bold).SetFontSize(11));
            doc.Add(new Paragraph("Text final.").SetFont(regular).SetFontSize(11));
        });

        var result = PdfConverter.Convert(pdf);

        Assert.True(result.HasText);
        Assert.Equal(1, result.PageCount);
        Assert.Equal(
            "# Títol del document\n\n" +
            LongText + "\n\n" +
            "## Secció primera\n\n" +
            "### Apartat en negreta\n\n" +
            "Text final.",
            result.Markdown);
    }

    [Fact]
    public void Page_numbers_are_dropped_and_pages_separated()
    {
        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        string pdf = NewPdf(doc =>
        {
            for (int i = 1; i <= 3; i++)
            {
                if (i > 1) doc.Add(new iText.Layout.Element.AreaBreak());
                doc.Add(new Paragraph($"Contingut {i}.").SetFont(font).SetFontSize(11));
                doc.ShowTextAligned(
                    new Paragraph(i.ToString()).SetFont(font).SetFontSize(9),
                    297, 30, i, iText.Layout.Properties.TextAlignment.CENTER,
                    iText.Layout.Properties.VerticalAlignment.BOTTOM, 0);
            }
        });

        var result = PdfConverter.Convert(pdf);

        Assert.Equal("Contingut 1.\n\n---\n\nContingut 2.\n\n---\n\nContingut 3.", result.Markdown);
    }

    [Fact]
    public void Pdf_without_text_reports_no_text()
    {
        string pdf = NewPdf(doc => doc.GetPdfDocument().AddNewPage());

        var result = PdfConverter.Convert(pdf);

        Assert.False(result.HasText);
        Assert.Equal(string.Empty, result.Markdown);
    }

    [Fact]
    public void User_password_protected_pdf_throws_password_exception()
    {
        var props = new WriterProperties().SetStandardEncryption(
            Encoding.ASCII.GetBytes("usuari"), Encoding.ASCII.GetBytes("propietari"),
            EncryptionConstants.ALLOW_PRINTING, EncryptionConstants.ENCRYPTION_AES_128);
        string pdf = NewPdf(doc => doc.Add(new Paragraph("Secret.")), props);

        Assert.Throws<PdfPasswordException>(() => PdfConverter.Convert(pdf));
    }

    [Fact]
    public void Owner_password_only_aes_pdf_converts()
    {
        var props = new WriterProperties().SetStandardEncryption(
            null, Encoding.ASCII.GetBytes("propietari"),
            EncryptionConstants.ALLOW_PRINTING, EncryptionConstants.ENCRYPTION_AES_256);
        string pdf = NewPdf(doc => doc.Add(new Paragraph("Text visible.")), props);

        Assert.Equal("Text visible.", PdfConverter.Convert(pdf).Markdown);
    }
}
