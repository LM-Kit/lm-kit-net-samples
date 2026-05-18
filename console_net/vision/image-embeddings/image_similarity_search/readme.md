# Visual Similarity & Near-Duplicate Index

Interactive console app that embeds a folder of images into a persistent vector index, then either clusters near-duplicates above a cosine threshold or runs visual-similarity search against a query image. Built on `LM.LoadFromModelID("nomic-embed-vision")`, `Embedder`, `DataSource`, and `VectorSearch`.

## What it shows

- `Embedder.GetEmbeddings(Attachment)` for image vectors.
- `DataSource.CreateFileDataSource(...)` for a persistent file-based vector index.
- `VectorSearch.FindMatchingPartitions(dataSources, vector)` for nearest-neighbour search by cosine.
- Four interactive modes from a menu:
  - **Index**: walk a folder, embed every image, write the index file.
  - **Duplicates**: cluster near-duplicates above a chosen cosine threshold; emits `duplicates_report.md` + `duplicates.csv`; reports reclaimable disk space.
  - **Search**: REPL of query-image paths; each returns top-K matches with similarity scores.
  - **Stats**: image count, total bytes, embedding dimension.

## Run

```bash
cd console_net/vision/image-embeddings/image_similarity_search
dotnet run
```

No command-line arguments. Pick the mode from the menu, point at a folder, choose a threshold.

## Where this fits

Pixel-hash dedup misses crops, rescales, and re-encodes. Vector-embedding dedup finds them. The same index also supports "find images that look like this one", which is the basic primitive behind visual search and asset-library exploration.
