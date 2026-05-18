# Page Layout Inspector

Interactive console app that runs VLM-OCR on a page image and inspects the resulting layout tree: text elements, lines, paragraphs, bounding boxes.

## What it shows

- `VlmOcr(model, VlmOcrIntent.Markdown)`.
- `VlmOcr.Run(ImageBuffer)` returning `VlmOcrResult { PageElement, TextGeneration }`.
- `PageElement.DetectLines()` returns `List<LineElement>` with bounds.
- `PageElement.DetectParagraphs()` returns `List<ParagraphElement>`.
- `LineElement.Bounds`, `LineElement.Text`.
- Two interactive modes from a menu:
  - **Image**: inspect one page with optional CSV export.
  - **Folder**: inspect every image in a folder, produce one CSV per file.

## Run

```bash
cd console_net/document-intelligence/layout-understanding/page_layout_inspector
dotnet run
```

No command-line arguments. The OCR model loads once at startup. Pick the mode from the menu and follow the prompts.

## Where this fits

Layout primitives feed downstream pipelines: column-aware search, region-based extraction, table reconstruction, document compare, redaction (with `LineElement.Bounds` you know exactly where each word sits on the page).
