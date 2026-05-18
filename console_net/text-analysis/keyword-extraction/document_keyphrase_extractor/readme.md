# Keyword Extraction

A demo for extracting keywords from PDF files, images, and text documents using LM-Kit.NET **vision-language models (VLMs)**.

## Features

- **Vision-Language Models**: Extract keywords directly from scanned documents, photos, and image files using VLMs
- Extract keywords from documents, images, and text files
- Support for multiple VLMs: GLM-V, MiniCPM, Qwen 3.5, Gemma 4, Ministral 3
- Configurable keyword count
- Confidence scoring for extraction quality
- Performance metrics (processing time, word count)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5-18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a PDF, image, or text file
4. View the extracted keywords with confidence score

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, WebP
- Text: TXT, HTML

## Models

| Option | Model | Approx. VRAM |
|--------|-------|-------------|
| 0 | Z.ai GLM-V 4.6 Flash 10B | ~7 GB |
| 1 | MiniCPM o 4.5 9B | ~5.9 GB |
| 2 | Alibaba Qwen 3.5 2B | ~2 GB |
| 3 | Alibaba Qwen 3.5 4B | ~3.5 GB |
| 4 | Alibaba Qwen 3.5 9B (Recommended) | ~7 GB |
| 5 | Gemma 4 E4B | ~6 GB |
| 7 | Qwen 3.6 27B | ~18 GB |
| 8 | Mistral Ministral 3 8B | ~6.5 GB |
