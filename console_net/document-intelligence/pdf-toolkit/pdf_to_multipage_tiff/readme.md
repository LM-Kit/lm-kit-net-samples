# PDF to Multi-page TIFF Archive

Interactive console app that renders every selected page of a PDF into a single multi-page TIFF. One `.tif` per source, suitable for records management, fax pipelines, and long-term archival workflows.

## What it shows

- `PdfRenderer.SavePagesAsMultipageTiffAsync(input, output, options, progress, ct)` writes one TIFF containing every page.
- `PdfRenderOptions { Zoom, PixelFormat, PageRange }` is the universal render config.
- `PixelFormat = ImagePixelFormat.GRAY8` produces compact grayscale TIFFs for archival scans.
- Two interactive modes from a menu:
  - **File**: prompt for input PDF, output `.tif`, zoom, grayscale, page range.
  - **Folder**: convert every PDF in a folder, one TIFF per source.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_to_multipage_tiff
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Multi-page TIFF is the canonical "one raster file per document" format in records management, government workflows, legal hold, and fax pipelines. Producing it locally keeps PII and case material out of cloud conversion services.

For one image file per page, see [pdf_pages_to_thumbnails](../pdf_pages_to_thumbnails/readme.md).
