# PDF Merger Batch

Interactive console app that combines multiple PDFs into a single packet. Either type the paths in order, or point at a folder and let the demo enumerate every PDF inside.

## What it shows

- `PdfInfo.GetPageCountAsync(path)` for fast input validation.
- `PdfMerger.MergeFilesAsync(inputs, output, ct)` for the actual merge.
- Two interactive modes from a menu:
  - **List**: paste paths one per line, then provide the output path.
  - **Folder**: point at a folder, choose recursion, choose sort order (name or mtime), then merge.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_merger_batch
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Closing a financial month, assembling a signed contract bundle, or producing a case file packet is mechanically a series of merges. Doing it locally avoids round-tripping confidential pages through an online merge service.
