# Multimodal Embeddings (Text + Image)

Interactive console app that embeds text and images into the same vector space with one model, Google EmbeddingGemma 2 (`embeddinggemma-2`), and exposes three cross-modal workflows.

## What it shows

- One `Embedder` over `embeddinggemma-2` for both modalities.
- `Embedder.GetQueryEmbeddings(string)` for the text side (captions, queries, tags), with the model's trained query prompt.
- `Embedder.GetEmbeddings(ImageBuffer)` for image embedding.
- `Embedder.GetCosineSimilarity(a, b)` for the cross-modal score.
- Three interactive modes from a menu:
  - **Matrix**: type captions + image paths, print the full cosine similarity matrix.
  - **Search**: text-over-images. Embed a folder of images once, run repeated text queries.
  - **Tag**: image-over-tags. Embed one image, rank candidate tags.

## Run

```bash
cd console_net/text-analysis/embeddings/multimodal_embeddings
dotnet run
```

No command-line arguments. The model (439 MB) downloads on first run and loads once at startup.

## Where this fits

Text-to-image search, image-to-text classification, and reverse image search collapse to one operation: cosine similarity in a shared embedding space. A model that embeds both text and images makes this possible without any per-query VLM call.
