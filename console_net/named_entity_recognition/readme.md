# Named Entity Recognition

A demo for extracting named entities from PDF files, images, and documents using LM-Kit.NET vision-language models. Identify people, organizations, locations, and other entities.

## Features

- Detect and extract named entities from documents
- Support for PDF files, images, and text documents
- Confidence scoring for each detected entity
- Support for multiple VLMs: MiniCPM, Qwen 3, Gemma 3, Ministral
- Processing time metrics

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5�12 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a document, image, or text file
4. View detected entities with confidence scores

## Entity Types

- **Person**: Names of individuals
- **Organization**: Companies, institutions, agencies
- **Location**: Cities, countries, addresses
- **Date**: Temporal references
- **Money**: Currency amounts
- **Product**: Product names
- **Event**: Named events
- And other entity categories

## Example Output

```
5 detected entities | processing time: 00:00:03.12

Person: "John Smith" (confidence=0.96)
Organization: "Acme Corporation" (confidence=0.94)
Location: "New York City" (confidence=0.98)
Date: "January 15, 2024" (confidence=0.92)
Money: "$50,000" (confidence=0.89)
```

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, WebP
- Text: TXT, HTML

## Use Cases

- Information extraction from documents
- Content indexing and search
- Knowledge graph construction
- Document analysis and classification