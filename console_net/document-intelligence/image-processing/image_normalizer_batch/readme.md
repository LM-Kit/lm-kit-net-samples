# Image Normalizer Batch

Interactive console app that resizes, rotates, and auto-crops images for vision / OCR pipelines. Single image or whole folder; every operation is optional.

## What it shows

- `ImageBuffer.LoadAsRGB(path)` and `ImageBuffer.SaveAsPng(path)`.
- `ImageBuffer.Resize(targetWidth, targetHeight)`.
- `ImageBuffer.Rotate(int degrees)`.
- `ImageBuffer.CropAuto(margin)` to trim uniform borders.
- Two interactive modes from a menu:
  - **File**: process a single image with chosen options.
  - **Folder**: process every supported image (`.png`, `.jpg`, `.jpeg`, `.bmp`, `.webp`, `.tif`, `.tiff`) in a folder.

## Run

```bash
cd console_net/document-intelligence/image-processing/image_normalizer_batch
dotnet run
```

No command-line arguments. Pick the mode from the menu, then choose which operations to apply (thumbnail size, rotation, auto-crop).

## Where this fits

Every VLM, OCR engine, and image-embedding model has an input budget. Resizing, rotating, and cropping right before inference keeps token counts predictable and recognition accuracy high. Field-collected images especially benefit from a normalization pass before they hit the model.
