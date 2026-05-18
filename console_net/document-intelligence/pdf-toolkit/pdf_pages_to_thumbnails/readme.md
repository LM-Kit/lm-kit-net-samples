# PDF Pages to Image Thumbnails

Interactive console app that renders PDF pages to one image file per page. Three preset profiles cover the common cases, plus a custom mode for full control over zoom, format, grayscale, and page range.

## What it shows

- `PdfRenderer.RenderPagesAsync(input, options, ct)` lazily yields `(pageIndex, ImageBuffer)` tuples.
- `PdfRenderOptions { Zoom, PixelFormat, PageRange }` is the universal render config.
- Per-format encoders: `SaveAsPng/Jpeg/Webp/Bmp/Tiff/Tga/Pnm` on the produced `ImageBuffer`.
- Four interactive modes from a menu:
  - **Thumbs**: 1x JPEG q=80 (cheap thumbnails for browsing).
  - **Preview**: 2x PNG (legible page previews for UIs).
  - **Archival**: 4x TIFF (high-resolution archive).
  - **Custom**: prompts for zoom, format, grayscale, page range.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_pages_to_thumbnails
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Per-page thumbnails feed document management UIs, classification preview, mail-room triage, and OCR pre-processing. Format choice depends on the consumer: PNG for web UIs, JPEG / WebP for compact preview, grayscale TIFF for archival scans. For packing every rendered page into one file, see [pdf_to_multipage_tiff](../pdf_to_multipage_tiff/readme.md).
