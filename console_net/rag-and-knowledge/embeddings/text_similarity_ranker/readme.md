# Semantic Search Over a Text Corpus

Interactive console app that indexes a folder of `.md` / `.txt` files (paragraph-level chunks with file:line attribution), then answers semantic queries with top-K results and an optional CSV export. Built on LM-Kit.NET embeddings.

## What it shows

- `LM.LoadFromModelID(...)` with picker for `qwen3-embedding:0.6b` / `4b` / `8b` / `embeddinggemma-300m`.
- `Embedder.GetEmbeddings(string[])` for batched chunk embedding (high throughput).
- `Embedder.GetQueryEmbeddings(text)` for the asymmetric query side.
- `Embedder.GetCosineSimilarity(a, b)` for ranking.
- Paragraph-boundary chunker with file + start/end-line metadata so every hit can show provenance.
- Four interactive modes from a menu:
  - **Index**: walk a folder, chunk, embed in 32-at-a-time batches.
  - **Demo**: load a built-in 10-passage corpus for a quick spin.
  - **Search**: REPL of queries; top-K with `file:lines` + snippet; optional CSV per query.
  - **Stats**: chunk count, file count, character total, embedding dimension.

## Run

```bash
cd console_net/rag-and-knowledge/embeddings/text_similarity_ranker
dotnet run
```

Pick a model, then index (option 1) or demo (option 2), then search (option 3). No command-line arguments.

## Where this fits

Keyword search misses "RAM keeps climbing" when the question is "memory leak". Semantic search hits it. This demo is the smallest end-to-end pipeline that does it correctly: chunk a folder, batch-embed every chunk, and rank queries by cosine similarity with attribution back to the source line range.
