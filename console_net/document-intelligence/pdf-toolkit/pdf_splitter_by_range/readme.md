# PDF Splitter by Page Range

Interactive console app that splits a PDF into smaller files. Either supply custom page ranges, or ask the demo to split every N pages.

## What it shows

- `PdfInfo.GetPageCountAsync(path)` to size the input.
- `PdfSplitter.SplitToFilesAsync(input, ranges, outDir, prefix, progress, ct)` for the actual split, with per-part progress reporting via `PdfSplitterProgressEventArgs`.
- Two interactive modes:
  - **Ranges**: type ranges like `1-3`, `4,6`, `7-9` one per line.
  - **Every N pages**: choose chunk size, the demo auto-generates the range list.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_splitter_by_range
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Bursting a quarterly report into per-section PDFs, exporting individual chapters of a manual, or chopping a 400-page contract into per-clause attachments are all repeating tasks. The SDK does the page-stream surgery for you; the demo wraps it in a friendly prompt.
