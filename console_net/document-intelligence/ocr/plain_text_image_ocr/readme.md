# OCR

Vision-language OCR pipeline. Loads a compact VLM-OCR model and runs `VlmOcrIntent.PlainText` on each input image.

## What it shows

- `LM.LoadFromModelID("paddleocr-vl-1.6:0.9b")` for the smallest OCR-capable VLM in the catalog.
- `new VlmOcr(model, VlmOcrIntent.PlainText)`.
- `VlmOcr.Run(ImageBuffer)` returning `VlmOcrResult { TextGeneration, PageElement }`.
- Try other intents: `TableRecognition`, `Markdown`, `Formulas`, `BoundingBoxes`.

## Run

```bash
cd console_net/document-intelligence/ocr/ocr_demo
dotnet run -- C:\scans\receipt.png C:\scans\letter.jpg
```

## Where this fits

OCR is the front door of every document pipeline. VLM-OCR catches handwriting, watermarks, low-resolution scans, and rotated images that classical OCR engines mangle.
