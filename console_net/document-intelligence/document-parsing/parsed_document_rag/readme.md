# Q&A over Parsed Documents, with Page Citations

Answers questions over a folder of documents parsed by LM-Kit.NET's AI document parser. Tables,
chart data and headings survive the parse, each page is indexed as its own section, and every
answer lists the pages it came from, fully on-device.

## Features

- `DocumentParser` output indexed with Markdown-aware chunking (`MarkdownChunking`).
- One RAG section per page, with `source` and `page` metadata: answers cite their pages.
- Running heads and feet left out of the index (`ParsedPage.Body`).
- Parse once: each parse is stored as JSON and re-read on the next run with `ParsedDocument.FromJson`, without the parser's models.
- The parser is released before the chat model loads, so the two never share the GPU.

## Prerequisites

- .NET 8.0 or later
- A GPU is recommended; the chat model's VRAM is listed in the menu (about 7 GB for the default)
- About 2.5 GB of disk for the parser's models, plus the embedding and chat models

## How It Works

1. Documents without a stored parse are parsed at `Medium` effort; each parse is saved as `<file>.parsed.json`.
2. Each page's body renders to Markdown and is imported with `RagEngine.ImportText`.
3. A question retrieves the best passages (`FindMatchingPartitions`) and the chat model answers from them.

## Usage

```bash
dotnet run
```

1. Enter a folder of documents.
2. Choose the chat model.
3. Ask questions; an empty line quits.

## Example Output

```
Question (empty to quit): On which benchmarks is the trace-based JIT the fastest, and by how much?

Answer: The trace-based JIT is the fastest on 9 of the 26 benchmarks: 3d-morph, bitops-3bit-bits-in-byte, bitsops-bitwise-and, crypto-shal, math-cordic, math-partial-sums, math-spectral-norm, string-base64, and string-validate-input.

For one of the benchmark programs, it achieved up to a 25x speedup on bitops-bitwise-and compared to the SpiderMonkey interpreter, and was almost 5 times faster than V8 and SFX.

Sources:
  paper.pdf p.11
  paper.pdf p.10
  paper.pdf p.12
  paper.pdf p.1
```

## Configuration

| Setting | Where | Effect |
|---|---|---|
| Effort level | `Effort` constant | `Medium` by default |
| Chunk size | `MarkdownChunking.MaxChunkSize` | 500 tokens |
| Retrieval depth | `topK` | 5 passages per question |

## Learn More

- [Index Parsed Documents for RAG with Page Citations](https://docs.lm-kit.com/lm-kit-net/guides/how-to/index-parsed-documents-for-rag.html)
