# Document to Markdown

A demo for converting PDF files, images, and scanned documents to Markdown using LM-Kit.NET vision-language models.

## Features

- Convert documents and images to structured Markdown text
- Support for multiple VLMs: LightOnOCR, MiniCPM, Qwen 3, Qwen 3.5, Gemma 3, Ministral
- Multi-page document processing
- Preserves document structure, tables, and formatting
- Real-time performance statistics (speed, token usage, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a PDF or image file
4. View the extracted Markdown output

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX (single and multi-page)
- Images: PNG, JPG, TIFF, BMP, and other common formats
- Scanned documents