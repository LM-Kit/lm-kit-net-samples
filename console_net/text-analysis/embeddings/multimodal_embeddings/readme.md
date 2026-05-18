# Multimodal Embeddings (Text + Image)

Interactive console app that embeds text and images into the same vector space (`nomic-embed-text` + `nomic-embed-vision`) and exposes three cross-modal workflows.

## What it shows

- Two aligned `Embedder` instances sharing one embedding space.
- `Embedder.GetEmbeddings(IEnumerable<string>)` for text batch embedding.
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

No command-line arguments. Both models load once at startup.

## Where this fits

Text-to-image search, image-to-text classification, and reverse image search collapse to one operation: cosine similarity in a shared embedding space. Aligned models make this possible without any per-query VLM call.
