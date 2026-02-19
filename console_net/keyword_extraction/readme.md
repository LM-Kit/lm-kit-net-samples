# Keyword Extraction

A demo for extracting keywords from PDF files, images, and text documents using LM-Kit.NET language models.

## Features

- Extract keywords from documents, images, and text files
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Configurable keyword count
- Confidence scoring for extraction quality
- Performance metrics (processing time, word count)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (0.8�16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter the path to a PDF, image, or text file
4. View the extracted keywords with confidence score

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, WebP
- Text: TXT, HTML