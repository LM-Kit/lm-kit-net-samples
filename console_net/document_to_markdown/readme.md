# Document to Markdown (Universal Conversion Engine)

End-to-end demonstration of LM-Kit.NET's **`DocumentToMarkdown`** converter, a state-of-the-art
universal document-to-Markdown engine that turns any office file, email, web page, PDF, or image
into clean, LLM-ready Markdown without leaving your .NET process.

## What This Demo Shows

- Universal input coverage: **PDF, DOCX, PPTX, XLSX, EML, MBOX, HTML, TXT**, and every common
  image format (PNG, JPG, TIFF, BMP, WEBP, GIF).
- Three interchangeable conversion strategies:
  - **TextExtraction**. Zero-model, pure text-layer extraction (fastest, deterministic).
  - **VlmOcr**. Vision-language OCR that rasterizes each page and transcribes it while
    preserving headings, tables, lists, and code blocks.
  - **Hybrid** (recommended). Per-page routing that keeps born-digital pages on the fast
    text-layer path and escalates scanned or image-heavy pages to the VLM.
- Format-aware specialized converters: EML / MBOX / HTML / DOCX bypass the page pipeline and
  produce structurally rich Markdown in a single pass.
- Live per-page telemetry via the `PageStarting` / `PageCompleted` events (planned strategy,
  elapsed time, tokens, quality score, warnings).
- Optional **YAML front matter**, page separators, HTML-table rewriting, and page-range
  selection (e.g. `"1-5, 7, 9-12"`).
- Direct-to-disk conversion via `ConvertToFile`.
- Traditional OCR fallback (`LMKitOcr`) plugged into the TextExtraction strategy for pure
  image inputs when you want to avoid loading a VLM.

## Why It Matters

LM-Kit.NET's document conversion pipeline is designed as a single entry point that replaces a
whole stack of legacy components: PDF text extractors, Tesseract-style OCR, DOCX/XLSX parsers,
email ripping libraries, and HTML-to-Markdown converters. One API, one result type, one
unified quality signal, fully local.

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- For VLM strategies: ~2 GB VRAM (LightOnOCR 2 1B is the default)

## Usage

```bash
dotnet run
```

1. Pick a strategy (`Hybrid`, `TextExtraction`, or `VlmOcr`).
2. If vision is needed, choose a vision-language model (or paste a custom URI).
3. Enter a document path. The loop supports any number of files per session.
4. Optional: provide a page range such as `1-5,7,10-12`.
5. Optional: provide an output `.md` path to save the Markdown to disk.

The demo prints per-page progress, a full Markdown preview, and a summary block with
requested/effective strategy, page breakdown, token totals, and timings.

## Quick API Reference

```csharp
using LMKit.Document.Conversion;
using LMKit.Model;

// Zero-config: lightonocr-2:1b is loaded lazily on first VLM page.
var converter = new DocumentToMarkdown();

// Or bring your own vision model.
var model = LM.LoadFromModelID("lightonocr-2:1b");
var converter2 = new DocumentToMarkdown(model);

var result = converter.Convert("report.pdf", new DocumentToMarkdownOptions
{
    Strategy = DocumentToMarkdownStrategy.Hybrid,
    PageRange = "1-10",
    EmitFrontMatter = true,
    PreferMarkdownTablesForNonNested = true
});

File.WriteAllText("report.md", result.Markdown);
```

## Supported Formats

| Category  | Formats                                                                 |
|-----------|-------------------------------------------------------------------------|
| Documents | PDF, DOCX, PPTX, XLSX, TXT                                              |
| Email     | EML, MBOX                                                               |
| Web       | HTML                                                                    |
| Images    | PNG, JPG, JPEG, TIFF, BMP, WEBP, GIF                                    |

## Model Menu (vision strategies)

| # | Model                            | VRAM    |
|---|----------------------------------|---------|
| 0 | LightOn LightOnOCR 2 1B (default) | ~2 GB   |
| 1 | Z.ai GLM-OCR 0.9B                 | ~1 GB   |
| 2 | Z.ai GLM-V 4.6 Flash 10B          | ~7 GB   |
| 3 | MiniCPM o 4.5 9B                  | ~5.9 GB |
| 4 | Alibaba Qwen 3.5 2B               | ~2 GB   |
| 5 | Alibaba Qwen 3.5 4B               | ~3.5 GB |
| 6 | Alibaba Qwen 3.5 9B               | ~7 GB   |
| 7 | Google Gemma 4 E4B                | ~6 GB   |
| 8 | Alibaba Qwen 3.6 27B              | ~18 GB  |
| 9 | Mistral Ministral 3 8B            | ~6.5 GB |

Any other input is treated as a custom model URI (local path, HTTP, or Hugging Face link).
