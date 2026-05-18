# Cross-Encoder Reranker Lab

Interactive console app that prints embedding-only and cross-encoder-reranked rankings side-by-side for the same query and corpus. Built-in sample, custom typed corpus, or load passages from a file.

## What it shows

- `LM.LoadFromModelID("qwen3-embedding:0.6b")` and `LM.LoadFromModelID("bge-m3-reranker")`.
- `Embedder.GetQueryEmbeddings`, `GetEmbeddings`, `GetCosineSimilarity`.
- `Reranker.GetScore(query, IEnumerable<string>)` batched cross-encoder.
- Two-stage retrieval shape: cheap embedding pass first, slow reranker second.
- Three interactive modes from a menu:
  - **Demo**: built-in 9-passage corpus + typed query.
  - **Custom**: type your own query and passages.
  - **File**: load passages from a UTF-8 file (one per line), optional CSV export.

## Run

```bash
cd console_net/rag-and-knowledge/reranker/cross_encoder_reranker
dotnet run
```

No command-line arguments. Both models load once at startup.

## Where this fits

Embedding similarity is a coarse signal. Cross-encoder reranking is the cheapest, highest-impact precision boost in a RAG pipeline. The side-by-side rendering makes the lift visible immediately on your own data.
