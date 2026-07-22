# PDF to PDF/A Converter

Interactive console app that converts existing PDF documents to the PDF/A archival format (ISO 19005). The converter repairs the document in place: fonts are embedded, device colours are calibrated, prohibited constructs are removed, and archival metadata is rebuilt, so text, vector graphics, and layout are preserved. Documents the rewrite cannot repair fall back to rendered pages with an invisible text layer, guaranteeing a conforming, searchable result. Everything runs on-device; no model download is required.

## What it shows

- `PdfAConverter.ConvertToFileAsync(inputPath, outputPath, options, ct)` for the end-to-end conversion.
- `PdfAConversionOptions { Level = PdfA1b | PdfA2b | PdfA3b, Fallback, Password, RasterDpi, RasterJpegQuality, IncludeInvisibleTextLayer }`.
- `PdfAConversionReport` inspection: conformance verdict, detected features, applied fixes, raster-fallback usage, removed encryption, unresolved violations.
- `PdfAConversionOptions.FallbackBehavior`: guarantee conformance via rasterization, fail fast, or convert best-effort and report what remains.
- Two interactive modes from a menu:
  - **File**: convert one PDF, prompting for conformance level, fallback behavior, and password.
  - **Folder**: convert every `.pdf` in a folder with a per-file outcome summary.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_to_pdfa_converter
dotnet run
```

No command-line arguments and no model download. Pick the mode from the menu and follow the prompts.

## Example session

```
> 1
Path to a PDF file: C:\docs\invoice.pdf
Output path [C:\docs\invoice_pdfa.pdf]:
Conformance level: 1=PDF/A-1b, 2=PDF/A-2b (default), 3=PDF/A-3b:
For content that still can't conform: 1=rasterize the rest (default), 2=fail, 3=report only:
Password (empty for unencrypted source):

  invoice.pdf -> invoice_pdfa.pdf
  Level            : PdfA2b
  Conforms         : True
  Pages            : 3
  Strategy         : conforming rewrite (content preserved)
  Features detected: UnembeddedFont, DeviceCmyk
  Fixes applied    : MiscKeys, FontsEmbedded, DefaultCmykInstalled
  Elapsed          : 412 ms
```

## Where this fits

PDF/A is the mandated format for long-term preservation in legal archiving, government records, invoicing regulations, and compliance workflows. Converting at ingestion time means every archived document is self-contained (fonts embedded, colours defined, no external dependencies) and stays reproducible across decades and viewers. The report tells you exactly what was changed, which supports audit trails, and the raster fallback guarantees that even damaged or exotic documents end up conforming instead of being rejected.
