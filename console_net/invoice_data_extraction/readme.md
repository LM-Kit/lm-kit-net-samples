# Invoice Data Extraction

A demo showcasing structured data extraction from invoice documents using LM-Kit.NET vision-language models. Supports PDF files, PNG, JPG, and other image formats.

## Features

- Extract invoice fields (vendor, date, totals, line items, etc.) from PDF and image documents
- Support for multiple VLMs: MiniCPM, Qwen 3, Gemma 3, Pixtral
- Optional OCR engine integration for improved accuracy on scanned documents
- Automatic language and orientation detection
- JSON output for easy integration

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5–12 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Choose a sample invoice or provide a custom PDF/image file path
4. View extracted data and JSON output

## Schema Configuration

Extraction fields are defined in `schema.json`. Modify this file to customize which data points are extracted from your invoice documents.

## Sample Invoices

The `examples/` folder contains sample invoice images in French, Spanish, and English for testing.