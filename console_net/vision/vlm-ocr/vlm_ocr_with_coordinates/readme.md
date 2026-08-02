# VLM OCR with Coordinates

A demo for extracting positioned regions from images and documents using LM-Kit.NET vision-language OCR models, and drawing the detected regions onto annotated output images. Depending on the selected model, the demo performs line-level text spotting or full semantic layout analysis with one color per region category.

## Features

- Detect regions with bounding-box coordinates using vision-language models
- Full layout analysis with Infinity-Parser2: every region is classified (title, text, table, formula, figure, captions, footnotes, header, footer) and exposed through `TextElement.Category`
- Category-aware rendering: each layout category is drawn with its own border color, with a matching color legend in the console
- Typed region content: tables carry HTML, formulas carry LaTeX, other categories carry Markdown
- Figure regions are reported with their bounding box even though they carry no text
- Line-level text spotting with PaddleOCR VL
- Support for images (PNG, JPEG, TIFF, BMP, WebP) and multi-page documents (PDF)
- Per-region output: category, content preview, position (x, y), and size (width, height)
- Real-time performance statistics (speed, token usage, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (~1 GB for PaddleOCR VL 1.6, ~2 GB for Infinity-Parser2 Flash)

## Usage

1. Run the application
2. Select a model:
   - **PaddleOCR VL 0.9B**: line-level text spotting
   - **Infinity-Parser2 Flash 2B**: full layout analysis with region categories
3. Enter the path to an image or document file
4. View the detected regions with categories and coordinates in the console
5. Find the annotated image saved next to the original file, with one border color per category

## How It Works

The demo queries `VlmOcr.GetSupportedIntents(model)` and picks the richest spatial intent the loaded model supports:

- **`VlmOcrIntent.LayoutAnalysis`** (Infinity-Parser2): the model runs its native doc2json task and returns every layout element with a bounding box, a semantic category, and typed content (HTML for tables, LaTeX for formulas, Markdown otherwise), sorted in human reading order. LM-Kit.NET parses this payload into `TextElement` instances whose `Category` property carries the classification, and maps the normalized grid coordinates back to the original image's pixel space through the preprocessing transform chain. Text-free regions (figures) keep their bounding box. The raw machine-readable JSON payload remains available through `VlmOcrResult.NormalizedText`.
- **`VlmOcrIntent.OcrWithCoordinates`** (PaddleOCR VL): the model emits each text line followed by eight normalized location tokens (four corners). LM-Kit.NET translates the tokens back to source-image pixel coordinates.

For each detected region, the demo:

- Prints the category tag (colored), a one-line content preview, and the bounding box
- Draws a rectangle on the image using the `Canvas` drawing API, colored by category
- Prints a legend mapping the categories present on the page to their colors
- Saves the annotated result as a PNG file

For multi-page documents (e.g. PDF), each page is processed and annotated individually.

## Region Colors (Layout Analysis)

| Category | Color |
|----------|-------|
| Title | Purple |
| Text | Blue |
| Table / captions | Green shades |
| Formula / captions | Teal shades |
| Figure / captions | Orange shades |
| Header / Footer / footnotes | Gray shades |
| Unclassified text line (spotting) | Red |

## Supported Formats

- Images: PNG, JPG, JPEG, TIFF, BMP, WebP
- Documents: PDF (multi-page)
