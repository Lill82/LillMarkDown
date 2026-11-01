# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build, test & run

```powershell
dotnet build                                  # Debug build (whole solution)
dotnet test                                   # xUnit v3 tests (Microsoft.Testing.Platform, see global.json)
dotnet run --project LillMarkDown             # run the app
dotnet publish LillMarkDown -p:PublishProfile=win-x64
# Output: LillMarkDown/bin/publish/<version>_<yyyyMMdd-HHmmss>/LillMarkDown.exe (single-file, self-contained)
# A new folder per publish; old builds are kept. CI pins it with -p:PublishDir=bin/publish/ci/
```

Run `dotnet test` after any change to the conversion rules. For changes the synthetic tests can't cover, also convert a few real PDFs and read the output.

## Architecture

Three projects, all .NET 10. Shared settings and product metadata (Version, Authors, Copyright…) live in `Directory.Build.props`.

**`LillMarkDown.Core/`** (`net10.0`, no UI). All conversion logic:
- `PdfLayoutExtractor` — custom iText `IEventListener` that captures every text fragment with its effective font size (Tf × text matrix × CTM), bold flag (font weight/name, or render mode 2) and position, then groups the fragments into `TextLine`s per page (`Layout.cs`). Rotated text is skipped.
- `MarkdownBuilder` — pure function `PageLayout[] → string`, so it can be tested without a PDF:
  1. drop running headers/footers: lines in the outer 8 % of the page that repeat on ≥ 40 % of pages (digits normalised), or that look like page numbers. Heading-size text is never dropped.
  2. document stats: body size = the size carrying the most characters; up to 3 larger sizes (≥ 1.15 × body) → `#`/`##`/`###`; per-size typical line spacing.
  3. group lines into blocks (paragraph / heading / list item) using style changes, vertical gaps, short lines, first-line indents and bullet markers.
  4. render: headings by size, then bold or all-caps short lines as a minor heading; lists; body text with leading Markdown syntax escaped; end-of-line hyphenation re-joined.
- `PdfConverter` — opens the PDF, returns `ConversionResult` (Markdown, PageCount, PagesWithText). Throws `PdfPasswordException` for user-password PDFs. Core produces no user-facing text.

**`LillMarkDown/`** (`net10.0-windows`, WPF). `MainWindow` — all UI, in Catalan: keyboard-accessible drop zone (a templated `Button`), files or folders (recursive), overwrite option, per-file progress, colour-coded log, "Obre la carpeta" link, version and AGPL source link in the footer.

**`LillMarkDown.Tests/`** (xUnit v3). `MarkdownBuilderTests` build synthetic `PageLayout`s; `PdfConverterTests` generate real PDFs with iText (including encrypted and blank ones) and convert them.

## Key constraints

- The repo-level `NuGet.Config` clears inherited sources and uses only nuget.org, so restore behaves the same on any machine (no machine-specific or private feeds).
- The repo is public on GitHub (`Lill82/LillMarkDown`): never commit organisation-specific names, internal URLs, emails or machine paths.
- `itext7.bouncy-castle-adapter` must stay referenced; without it iText cannot open AES-encrypted PDFs.
- The self-contained/single-file/RID settings in `LillMarkDown.csproj` are gated behind `Condition="'$(PublishProtocol)' != ''"`, so they only activate with a publish profile (which sets `PublishProtocol=FileSystem`). Debug/Run builds stay framework-dependent and fast. Plain `dotnet publish` without the profile will **not** produce the single-file exe.
- All user-facing text must remain in Catalan.

## Releases

`.github/workflows/build.yml` builds and tests every push and PR. Pushing a `v*` tag (e.g. `v1.1.0`) also publishes the exe with that version and attaches it to a GitHub Release. Bump `Version` in `Directory.Build.props` to match.

## Licence

AGPL-3.0-only — see `LICENSE`. Required because the app links iText 7 (AGPL); do not relicense to anything incompatible or swap in dependencies with AGPL-incompatible licences. The footer link to the source code is part of meeting the AGPL; keep it.
