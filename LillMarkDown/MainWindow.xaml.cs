using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LillMarkDown;

public partial class MainWindow : Window
{
    private static readonly SolidColorBrush StrokeNormal  = new(Color.FromRgb(176, 196, 222));
    private static readonly SolidColorBrush StrokeActive  = new(Color.FromRgb(26,  115, 232));
    private static readonly SolidColorBrush FillNormal    = new(Color.FromRgb(250, 252, 255));
    private static readonly SolidColorBrush FillActive    = new(Color.FromRgb(232, 240, 254));
    private static readonly SolidColorBrush ColorBlue     = new(Color.FromRgb(26,  115, 232));
    private static readonly SolidColorBrush ColorGreen    = new(Color.FromRgb(15,  157,  88));
    private static readonly SolidColorBrush ColorOrange   = new(Color.FromRgb(230,  81,   0));

    private bool _busy;

    public MainWindow() => InitializeComponent();

    // ── Esdeveniments d'arrossegament ─────────────────────────────────────

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
            SetZoneHighlight(true);
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(DropZoneGrid);
        var bounds = new Rect(0, 0, DropZoneGrid.ActualWidth, DropZoneGrid.ActualHeight);
        if (!bounds.Contains(pos))
            SetZoneHighlight(false);
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        SetZoneHighlight(false);
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            _ = ProcessFilesAsync(files);
        }
        e.Handled = true;
    }

    private void DropZone_Click(object sender, MouseButtonEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "Seleccioneu fitxers PDF per convertir",
            Filter      = "Fitxers PDF|*.pdf",
            Multiselect = true
        };
        if (dlg.ShowDialog() == true)
            _ = ProcessFilesAsync(dlg.FileNames);
    }

    // ── Conversió ─────────────────────────────────────────────────────────

    private async Task ProcessFilesAsync(IEnumerable<string> paths)
    {
        // Prevent concurrent conversions racing on the status/log UI.
        if (_busy) return;
        _busy = true;
        DropZoneGrid.IsEnabled = false;

        try
        {
            var pdfs = paths
                .Where(p => p.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (pdfs.Length == 0)
            {
                SetStatus("No hi ha fitxers PDF a la selecció.", ColorOrange);
                return;
            }

            SetStatus($"Convertint {pdfs.Length} fitxer(s)...", ColorBlue);

            int ok = 0, fail = 0;

            foreach (var pdf in pdfs)
            {
                string name = Path.GetFileName(pdf);
                try
                {
                    string markdown = await Task.Run(() => PdfConverter.Convert(pdf));
                    // Use a non-colliding name so we never silently overwrite existing output.
                    string outPath  = NonCollidingPath(Path.ChangeExtension(pdf, ".md"));
                    await File.WriteAllTextAsync(outPath, markdown);
                    AppendLog($"[OK]  {name}  →  {Path.GetFileName(outPath)}");
                    ok++;
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERR] {name}: {ex.Message}");
                    fail++;
                }
            }

            SetStatus(
                fail == 0
                    ? $"Fet — {ok} fitxer(s) convertits."
                    : $"Fet — {ok} convertits, {fail} amb errors.",
                fail == 0 ? ColorGreen : ColorOrange);
        }
        finally
        {
            _busy = false;
            DropZoneGrid.IsEnabled = true;
        }
    }

    // ── Auxiliars ─────────────────────────────────────────────────────────

    // Returns a path that does not yet exist, appending " (2)", " (3)" … as needed.
    private static string NonCollidingPath(string basePath)
    {
        if (!File.Exists(basePath)) return basePath;
        string dir  = Path.GetDirectoryName(basePath)!;
        string stem = Path.GetFileNameWithoutExtension(basePath);
        string ext  = Path.GetExtension(basePath);
        for (int n = 2; ; n++)
        {
            string candidate = Path.Combine(dir, $"{stem} ({n}){ext}");
            if (!File.Exists(candidate)) return candidate;
        }
    }

    private void SetZoneHighlight(bool active)
    {
        DropZoneRect.Stroke = active ? StrokeActive : StrokeNormal;
        DropZoneRect.Fill   = active ? FillActive   : FillNormal;
    }

    private void SetStatus(string text, SolidColorBrush color)
    {
        StatusText.Text       = text;
        StatusText.Foreground = color;
    }

    private void AppendLog(string line)
    {
        LogText.Text = string.IsNullOrEmpty(LogText.Text)
            ? line
            : LogText.Text + "\n" + line;
        LogScroller.ScrollToEnd();
    }
}
