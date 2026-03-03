# Language Detection from Document

A demo for detecting the language of PDF files, documents, and images using LM-Kit.NET **vision-language models (VLMs)**.

## Features

- **Vision-Language Models**: Detect language directly from scanned documents, photos, and image files using VLMs
- Detect language from PDF documents, DOCX, and image files
- Support for multiple VLMs: GLM-V, MiniCPM, Qwen 3.5, Gemma 3, Ministral 3
- Fast processing with performance metrics
- Multilingual support for a wide range of languages

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5-18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a PDF, document, or image file
4. View the detected language and processing time

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
| 5 | Gemma 3 4B | ~5.7 GB |
| 6 | Gemma 3 12B | ~11 GB |
| 7 | Qwen 3.5 27B | ~18 GB |
| 8 | Mistral Ministral 3 8B | ~6.5 GB |
