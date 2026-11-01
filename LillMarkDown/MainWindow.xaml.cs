using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LillMarkDown;

public partial class MainWindow : Window
{
    private const string SourceUrl = "https://github.com/Lill82/LillMarkDown";

    private static readonly SolidColorBrush StrokeNormal = new(Color.FromRgb(176, 196, 222));
    private static readonly SolidColorBrush StrokeActive = new(Color.FromRgb(26, 115, 232));
    private static readonly SolidColorBrush FillNormal   = new(Color.FromRgb(250, 252, 255));
    private static readonly SolidColorBrush FillActive   = new(Color.FromRgb(232, 240, 254));
    private static readonly SolidColorBrush ColorBlue    = new(Color.FromRgb(26, 115, 232));
    private static readonly SolidColorBrush ColorGreen   = new(Color.FromRgb(15, 157, 88));
    private static readonly SolidColorBrush ColorOrange  = new(Color.FromRgb(230, 81, 0));

    private bool _busy;
    private bool _dragging;
    private string? _lastOutput;

    public MainWindow()
    {
        InitializeComponent();

        var v = Assembly.GetExecutingAssembly().GetName().Version;
        VersionRun.Text = v is null ? string.Empty : $"v{v.Major}.{v.Minor}.{v.Build}";
    }

    // ── Esdeveniments d'arrossegament ─────────────────────────────────────

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = !_busy && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_DragEnter(object sender, DragEventArgs e)
    {
        if (!_busy && e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            _dragging = true;
            UpdateZoneHighlight();
        }
        e.Handled = true;
    }

    private void DropZone_DragLeave(object sender, DragEventArgs e)
    {
        var pos = e.GetPosition(DropZoneGrid);
        var bounds = new Rect(0, 0, DropZoneGrid.ActualWidth, DropZoneGrid.ActualHeight);
        if (!bounds.Contains(pos))
        {
            _dragging = false;
            UpdateZoneHighlight();
        }
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        _dragging = false;
        UpdateZoneHighlight();
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            _ = ProcessPathsAsync(paths);
        e.Handled = true;
    }

    private void DropZone_FocusChanged(object sender, KeyboardFocusChangedEventArgs e) => UpdateZoneHighlight();

    private void DropZone_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title       = "Seleccioneu fitxers PDF per convertir",
            Filter      = "Fitxers PDF|*.pdf",
            Multiselect = true
        };
        if (dlg.ShowDialog(this) == true)
            _ = ProcessPathsAsync(dlg.FileNames);
    }

    // ── Enllaços ──────────────────────────────────────────────────────────

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (_lastOutput is not null && File.Exists(_lastOutput))
            Process.Start("explorer.exe", $"/select,\"{_lastOutput}\"");
    }

    private void SourceLink_Click(object sender, RoutedEventArgs e) =>
        Process.Start(new ProcessStartInfo(SourceUrl) { UseShellExecute = true });

    // ── Conversió ─────────────────────────────────────────────────────────

    private async Task ProcessPathsAsync(IEnumerable<string> paths)
    {
        // Prevent concurrent conversions racing on the status/log UI.
        if (_busy) return;
        _busy = true;
        DropZone.IsEnabled = false;
        OverwriteCheck.IsEnabled = false;
        bool overwrite = OverwriteCheck.IsChecked == true;

        try
        {
            SetStatus("Cercant fitxers PDF...", ColorBlue);
            var pdfs = await Task.Run(() => ExpandToPdfs(paths));

            if (pdfs.Count == 0)
            {
                SetStatus("No hi ha fitxers PDF a la selecció.", ColorOrange);
                return;
            }

            int ok = 0, warn = 0, fail = 0;

            for (int i = 0; i < pdfs.Count; i++)
            {
                string pdf = pdfs[i];
                string name = Path.GetFileName(pdf);
                SetStatus($"Convertint {i + 1} de {pdfs.Count}: {name}", ColorBlue);

                try
                {
                    var result = await Task.Run(() => PdfConverter.Convert(pdf));

                    if (!result.HasText)
                    {
                        AppendLog($"[AVÍS] {name}: no conté text (PDF escanejat?). No s'ha creat cap fitxer.");
                        warn++;
                        continue;
                    }

                    string target = Path.ChangeExtension(pdf, ".md");
                    string outPath = overwrite ? target : NonCollidingPath(target);
                    await File.WriteAllTextAsync(outPath, result.Markdown + "\n");
                    _lastOutput = outPath;

                    int empty = result.PageCount - result.PagesWithText;
                    if (empty > 0)
                    {
                        AppendLog($"[AVÍS] {name}  →  {Path.GetFileName(outPath)}  ({empty} de {result.PageCount} pàgines sense text)");
                        warn++;
                    }
                    else
                    {
                        AppendLog($"[OK]  {name}  →  {Path.GetFileName(outPath)}");
                        ok++;
                    }
                }
                catch (PdfPasswordException)
                {
                    AppendLog($"[ERR] {name}: el PDF està protegit amb contrasenya.");
                    fail++;
                }
                catch (Exception ex)
                {
                    AppendLog($"[ERR] {name}: {ex.Message}");
                    fail++;
                }
            }

            SetStatus(Summary(ok, warn, fail), fail + warn == 0 ? ColorGreen : ColorOrange);
            OpenFolderLink.Visibility = _lastOutput is null ? Visibility.Collapsed : Visibility.Visible;
        }
        finally
        {
            _busy = false;
            DropZone.IsEnabled = true;
            OverwriteCheck.IsEnabled = true;
        }
    }

    private static string Summary(int ok, int warn, int fail)
    {
        var parts = new List<string> { $"{ok} convertit(s)" };
        if (warn > 0) parts.Add($"{warn} amb avisos");
        if (fail > 0) parts.Add($"{fail} amb errors");
        return "Fet — " + string.Join(", ", parts) + ".";
    }

    // ── Auxiliars ─────────────────────────────────────────────────────────

    private static readonly EnumerationOptions RecursivePdfSearch = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
        MatchCasing = MatchCasing.CaseInsensitive,
    };

    /// <summary>Files are kept if they are PDFs; folders are searched recursively.</summary>
    private static List<string> ExpandToPdfs(IEnumerable<string> paths)
    {
        var result = new List<string>();
        foreach (var path in paths)
        {
            if (Directory.Exists(path))
                result.AddRange(Directory.EnumerateFiles(path, "*.pdf", RecursivePdfSearch).Order());
            else if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(path))
                result.Add(path);
        }
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

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

    private void UpdateZoneHighlight()
    {
        bool active = _dragging || DropZone.IsKeyboardFocused;
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
