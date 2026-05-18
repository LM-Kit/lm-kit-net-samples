# Multi-Format to Markdown

Interactive console app that converts PDF, DOCX, PPTX, XLSX, HTML, EML, MBOX, and images to Markdown. Combines native text extraction with VLM-OCR fallback for scanned pages and image attachments.

## What it shows

- `DocumentToMarkdown(LM visionModel)` constructor.
- `DocumentToMarkdownOptions { Strategy, OcrEngine, OcrImageParallelism, IncludePageSeparators }`.
- `DocumentToMarkdown.Convert(path, options)` returning a `DocumentToMarkdownResult` with `Markdown`, `Pages`, `RequestedStrategy`, `EffectiveStrategy`, `Certainty`, `Elapsed`.
- Strategy modes: `Hybrid` (default), `TextExtraction`, `VlmOcr`.
- Two interactive modes from a menu:
  - **File**: convert one document.
  - **Folder**: convert every supported file in a folder (recursive).

## Run

```bash
cd console_net/document-intelligence/document-conversion/multi_format_to_markdown
dotnet run
```

No command-line arguments. The OCR model loads once at startup. Pick the mode from the menu, choose a strategy, and follow the prompts.

## Where this fits

Once your corpus is in Markdown, every downstream tool just works: search, RAG, embeddings, summarization, fine-tuning datasets, static site builds. The hybrid pipeline keeps native text where it exists (deterministic, fast) and falls back to VLM-OCR only where it has to.
