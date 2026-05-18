# Multipage TIFF to PDF/A Archive

Interactive console app that turns scanned multipage TIFFs into searchable, archival PDF/A files. The original raster stays as the visible layer; an invisible OCR text layer is overlaid so the output is selectable, copyable, and full-text searchable while remaining a faithful visual archive.

## What it shows

- `ImageToSearchablePdf.ConvertAsync(tiffPath, ocrEngine, outputPdf, options, ct)` for the end-to-end pipeline.
- `LMKitOcr` as the on-device OCR engine (Tesseract-based, no model download required for default English).
- `PdfGenerationOptions { Version = PdfA1b | PdfA2b | PdfA3b, Languages, PageRange, MaxDegreeOfParallelism, Progress, Creator, Producer, EnableOrientationDetection }`.
- Per-page progress via `IProgress<OcrProgressEventArgs>`.
- Two interactive modes from a menu:
  - **File**: convert one multipage TIFF, prompts for archive level, OCR languages, page range, parallelism.
  - **Folder**: convert every `.tif` / `.tiff` in a folder (optional recurse) and write a CSV manifest with bytes, pages, elapsed ms, and status per source.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/tiff_to_pdfa_archive
dotnet run
```

No command-line arguments. The OCR engine loads once at startup. Pick the mode from the menu and follow the prompts.

## Where this fits

Records management, fax inboxes, archival scans, government workflows, and legal hold all routinely ingest multipage TIFFs. PDF/A-1B (ISO 19005-1, level B) is the canonical long-term archival format: visual reproduction is guaranteed identical across decades and viewers. Adding a searchable text layer in the same step means the archive is also indexable for compliance, eDiscovery, and downstream RAG without re-OCRing later.
