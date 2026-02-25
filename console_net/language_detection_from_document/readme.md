# Language Detection from Document

A demo for detecting the language of PDF files and images using LM-Kit.NET vision-language models.

## Features

- Detect language from PDF documents and image files
- Support for multiple VLMs: MiniCPM, Qwen 3, Qwen 3.5, Gemma 3, Ministral
- Fast processing with performance metrics
- Multilingual support for a wide range of languages

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5~18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a PDF or image file
4. View the detected language and processing time

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, and other common formats
- Text: TXT, HTML