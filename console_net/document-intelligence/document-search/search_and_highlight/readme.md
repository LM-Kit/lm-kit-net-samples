# Search and Highlight Demo

A console application that demonstrates **SearchHighlightEngine** from LM-Kit.NET. Given a PDF or image, it searches for text and produces a highlighted copy with matches visually marked.

## Features

- Search text in **PDF documents** (uses the native text layer) or **images** (via OCR)
- Three search modes: **exact text**, **regex**, and **fuzzy** (Damerau-Levenshtein)
- Automatic OCR detection: triggers OCR only when the document contains no extractable text
- OCR engine selection: **LM-Kit OCR** (no model required) or **PaddleOCR-VL** (VLM-based, coordinate-aware)
- Outputs highlighted **PDF** (with highlight annotations) or **PNG** (with overlays)
- Auto-opens the result file when done

## Prerequisites

- .NET 8.0 or later
- For PaddleOCR-VL: ~1 GB VRAM (model downloaded automatically on first use)
- For LM-Kit OCR: no additional requirements (dictionaries downloaded automatically)

## How It Works

1. Provide a PDF or image file path
2. If the document has no extractable text, choose an OCR engine (LM-Kit OCR or PaddleOCR-VL)
3. Enter a search query
4. Select a search mode (Text, Regex, or Fuzzy)
5. The engine highlights all matches and saves the output
6. The highlighted file opens automatically

## Usage

```bash
dotnet run --project demos/console_net/document_processing/search_and_highlight
```

## Example Output

```
LM-Kit Search and Highlight Demo
Search text in PDFs and images, and produce highlighted output.

Assistant - enter PDF or image file path (or 'q' to quit):
> invoice.pdf

Assistant - enter search query:
> total

Select search mode:

  0 - Text  (exact substring match, default)
  1 - Regex (regular expression pattern)
  2 - Fuzzy (approximate matching)

> 0

---------- Results ----------
  Query        : "total"
  Search mode  : Text
  Pages scanned: 1/1
  Matches found: 3
  [1] Page 1: "Total"
  [2] Page 1: "Subtotal"
  [3] Page 1: "Total Due"

---------- Stats ----------
  Elapsed time : 0.12 s
----------------------------

Highlighted file saved to: invoice_highlighted.pdf
```

## Search Modes

| Mode  | Description                                    |
|-------|------------------------------------------------|
| Text  | Exact case-insensitive substring match         |
| Regex | Regular expression pattern matching            |
| Fuzzy | Approximate matching (Damerau-Levenshtein)     |

## Key Classes

- [`SearchHighlightEngine`](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Document.Search.SearchHighlightEngine.html) (LMKit.Document.Search): static API for search and highlight
- [`SearchHighlightOptions`](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Document.Search.SearchHighlightOptions.html): search mode, appearance, page range configuration
- [`SearchHighlightResult`](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Document.Search.SearchHighlightResult.html): output bytes, match metadata
- [`LMKitOcr`](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Extraction.Ocr.LMKitOcr.html) (LMKit.Extraction.Ocr): traditional OCR engine
- [`VlmOcr`](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Extraction.Ocr.VlmOcr.html) (LMKit.Extraction.Ocr): vision-language model OCR engine
