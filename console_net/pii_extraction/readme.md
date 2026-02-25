# PII Extraction

A demo for extracting Personally Identifiable Information (PII) from PDF files, images, and documents using LM-Kit.NET vision-language models.

## Features

- Detect and extract PII entities from documents
- Support for PDF files, images, and text documents
- Confidence scoring for each detected entity
- Support for multiple VLMs: MiniCPM, Qwen 3, Qwen 3.5, Gemma 3, Ministral
- Processing time metrics

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5~18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a document, image, or text file
4. View detected PII entities with confidence scores

## Detected Entity Types

- Names (personal, business)
- Email addresses
- Phone numbers
- Physical addresses
- Social security numbers
- Credit card numbers
- Dates of birth
- Account numbers
- And other PII categories

## Example Output

```
3 detected entities | processing time: 00:00:02.45

Name: "John Smith" (confidence=0.95)
Email: "john.smith@email.com" (confidence=0.98)
Phone: "+1-555-123-4567" (confidence=0.92)
```

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, WebP
- Text: TXT, HTML

## Use Cases

- Data privacy compliance (GDPR, CCPA)
- Document redaction preparation
- Sensitive data discovery
- Automated PII auditing