# Searchable PDF from Scans

Interactive console app that turns image-only scanned PDFs into searchable PDFs: identical visual output, plus an invisible text layer that makes the file selectable, copyable, and indexable.

## What it shows

- `LM.LoadFromModelID(...)` to load a VLM-OCR model (paddleocr-vl, glm-ocr, or lightonocr-2).
- `new VlmOcr(model, VlmOcrIntent.Markdown)` for layout-preserving OCR.
- `PdfSearchableMaker.ConvertToFileAsync(input, ocrEngine, output, options, ct)` for the conversion.
- `PdfSearchableMakerOptions { TextPageHandling, TextDetectionStrategy, MaxDegreeOfParallelism, Progress }`.
- Two interactive modes from a menu:
  - **File**: convert a single scanned PDF.
  - **Folder**: convert every PDF in a folder, one `.searchable.pdf` per source.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/searchable_pdf_from_scans
dotnet run
```

No command-line arguments. Pick the OCR model at startup, then pick a mode from the menu and follow the prompts.

## Where this fits

Document-management systems, search engines, and compliance pipelines cannot operate on image-only PDFs. PDF -> PDF/OCR is the most demanded conversion in the PDF toolkit. Done locally, it keeps PII inside the customer environment.
