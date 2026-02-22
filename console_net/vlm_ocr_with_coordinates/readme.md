# VLM OCR with Coordinates

A demo for extracting text regions with bounding boxes from images and documents using LM-Kit.NET vision-language OCR models, and drawing the detected regions onto annotated output images.

## Features

- Detect text regions with bounding-box coordinates using vision-language models
- Draw red bounding boxes around every detected region and save annotated images
- Support for images (PNG, JPEG, TIFF, BMP, WebP) and multi-page documents (PDF)
- Per-region output: text content, position (x, y), and size (width, height) in source image pixels
- Model selection menu structured for future expansion (currently PaddleOCR VL)
- Real-time performance statistics (speed, token usage, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (~1 GB for PaddleOCR VL 1.5)

## Usage

1. Run the application
2. Select a model (PaddleOCR VL 0.9B is the recommended default)
3. Enter the path to an image or document file
4. View the detected text regions with coordinates in the console
5. Find the annotated image saved next to the original file

## How It Works

The demo uses `VlmOcrIntent.OcrWithCoordinates` which instructs the OCR engine to emit text regions with location data. PaddleOCR VL encodes each region's position as eight normalized location tokens (four corners). LM-Kit.NET automatically translates these tokens back to the original image's pixel coordinate system through the preprocessing transform chain.

For each detected region, the demo:
- Prints the text content and bounding box (left, top, width, height)
- Draws a red rectangle on the image using the `Canvas` drawing API
- Saves the annotated result as a PNG file

For multi-page documents (e.g. PDF), each page is processed and annotated individually.

## Supported Formats

- Images: PNG, JPG, JPEG, TIFF, BMP, WebP
- Documents: PDF (multi-page)
