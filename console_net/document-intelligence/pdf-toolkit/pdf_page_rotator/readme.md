# PDF Page Rotator

Interactive console app that bakes per-page rotation into a PDF. Three modes: auto-fix sideways scans, rotate every page uniformly, or rotate only selected page numbers.

## What it shows

- `PdfInfo.GetPageInfoAsync(path, i).Orientation` returns the current `PageOrientations`.
- `PageEdit(int pageIndex, PageOrientations rotation)` describes a single per-page transform.
- `PdfEditor.ApplyToFileAsync(source, edits, outputPath, ct)` writes the rotated PDF.
- Three interactive modes from a menu:
  - **Auto**: scan every page, rotate any non-Normal page back to Normal.
  - **All**: rotate every page by 90, 180, or 270 degrees.
  - **Pages**: rotate only a typed list/range like `1,3-5,8`.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_page_rotator
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Mail-room scanners and field-collected scans frequently deliver PDFs with sideways pages. Auto-correcting orientation before OCR or indexing improves text recognition accuracy and reviewer ergonomics. Selective rotation handles the common case of a single inserted landscape page (a balance sheet, a foldout map) in an otherwise portrait document.
