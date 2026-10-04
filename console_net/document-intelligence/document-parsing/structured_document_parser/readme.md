# Structured Document Parser

Parses PDFs, scans and images with LM-Kit.NET's AI document parser, **`DocumentParser`**, and
exports each document as Markdown, lossless JSON, semantic HTML and DocLang, fully on-device.

## Features

- One setting, the **effort level** (`Low`, `Medium`, `High`); the parser chooses and verifies its models.
- Typed elements per page, in reading order: role, bounds, confidence and content.
- Tables as HTML with their merged cells, formulas as LaTeX, **charts as the data they plot**.
- Live progress through the `PageParsed` event, and page-range selection (`1-3, 7`).
- Exports: `ToMarkdown`, `ToJson`, `ToHtml`, `ToDocLang`, plus figure pictures and a `.dclx`
  DocLang archive with `IncludeFigureImages`.

## Prerequisites

- .NET 8.0 or later
- A GPU is recommended (about 6 GB of VRAM for `Medium`); the parser runs on the CPU too, more slowly
- About 2.5 GB of disk for the parser's models, downloaded on first run

## How It Works

1. The demo downloads the models the chosen effort level uses (`DocumentParser.GetModelCards`).
2. `new DocumentParser(effort)` loads them and verifies each one.
3. `parser.Parse(path, pageRange)` reads every selected page into a `ParsedDocument`.
4. The element tree is printed, and every export is written to `<document>_parsed/`.

## Usage

```bash
dotnet run
```

1. Choose the effort level.
2. Choose whether to keep the figure pictures.
3. Enter a document path, and optionally a page range.

## Example Output

```
--- Page 11  (612 x 792 Points)
    0  Figure          chart (data table)   [53,70 547,329] 100 %  | Characteristic | Tracing | SFX | V8 |
    1  FigureCaption   text                 [51,351 560,422] 100 %  **Figure 10.** Speedup vs. a baseline...
    2  Text            text                 [51,441 294,493] 100 %  In particular, the bitops benchmarks...

Exported Markdown, JSON, HTML and DocLang to reports\paper_parsed
```

## Configuration

| Setting | Where | Effect |
|---|---|---|
| Effort level | Startup menu | Time per page against fidelity |
| Figure pictures | Startup prompt | `IncludeFigureImages`: pictures as files and a `.dclx` archive |
| Page range | Per document | `Parse(path, "1-3, 7")` |

## Learn More

- [Parse Documents into Structured Elements](https://docs.lm-kit.com/lm-kit-net/guides/how-to/parse-documents-into-structured-elements.html)
- [Parsed-document JSON schema](https://docs.lm-kit.com/lm-kit-net/schemas/parsed-document/v1.json)
