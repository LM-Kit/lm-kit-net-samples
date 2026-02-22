# VLM OCR

A demo for extracting plain text from images and documents using LM-Kit.NET vision-language OCR models, with dedicated support for PaddleOCR VL task modes.

## Features

- Extract plain text from images, PDFs, and scanned documents using vision-language models
- Dedicated PaddleOCR VL mode selection: general OCR, table recognition, formula recognition, chart recognition, text spotting, seal recognition
- Support for multiple VLMs: PaddleOCR VL, LightOnOCR, MiniCPM, Qwen 3 VL, Gemma 3
- Multi-page document processing
- Real-time performance statistics (speed, token usage, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (1-6 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model (PaddleOCR VL 0.9B is the recommended default)
3. Enter the path to an image or document file
4. If using PaddleOCR VL, select the OCR task mode
5. View the extracted text output

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX (single and multi-page)
- Images: PNG, JPG, TIFF, BMP, and other common formats
- Scanned documents
