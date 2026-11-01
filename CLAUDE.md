# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```powershell
# Build (Debug)
dotnet build

# Run (Debug)
dotnet run --project LillMarkDown

# Build (Release)
dotnet build -c Release

# Publish self-contained single-file win-x64 exe
dotnet publish -p:PublishProfile=win-x64
# Output: LillMarkDown/bin/publish/LillMarkDown.exe
```

There are no automated tests — this project is verified manually by running the app and dropping in a PDF.

## Architecture

Single WPF project (`LillMarkDown/`) targeting `net8.0-windows`. Three meaningful files:

**`MainWindow.xaml/.xaml.cs`** — All UI (in Catalan). Accepts PDF files via drag-drop or a browse dialog, runs conversion on a background `Task.Run`, and reports progress into a color-coded log (`StatusText` + `LogText`). Three color states: blue (processing), green (success), orange (warnings/errors).

**`PdfConverter.cs`** — All conversion logic. Uses **itext7** (`LocationTextExtractionStrategy`) to extract text page-by-page in spatial order. Heading detection is heuristic only — itext7 doesn't expose font metrics at this abstraction level:
- All-caps line ≤ 80 chars → H2 (converted to Title Case)
- Short line ≤ 60 chars, no trailing punctuation, preceded by blank line → H3
- Everything else → body paragraph

Output is a `.md` file written alongside the source PDF, with `---` page separators.

## Key Constraints

- itext7 has no public font-size API on `LocationTextExtractionStrategy` — heading detection must stay heuristic unless the extraction strategy is replaced.
- The repo-level `NuGet.Config` clears inherited sources and uses only nuget.org, so restore behaves the same on any machine (no machine-specific or private feeds).
- The repo is public on GitHub (`Lill82/LillMarkDown`): never commit organisation-specific names, internal URLs, emails or machine paths.
- The publish profile produces a single self-contained `LillMarkDown.exe` (win-x64). The self-contained/single-file/RID settings are gated in the csproj behind `Condition="'$(PublishProtocol)' != ''"`, so they only activate when a publish profile is active (the profile sets `PublishProtocol=FileSystem`). This keeps Debug/Run builds framework-dependent and fast. Plain `dotnet publish` without a profile will **not** produce the single-file exe; always use `-p:PublishProfile=win-x64`.
- All user-facing text must remain in Catalan.

## Licence

AGPL-3.0-only — see `LICENSE`. Required because the app links iText 7 (AGPL); do not relicense to anything incompatible or swap in dependencies with AGPL-incompatible licences.
