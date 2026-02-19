# Document Classification

A demo for classifying PDF files, images, and documents into predefined categories using LM-Kit.NET vision-language models. Process single files or batch-process entire directories.

## Features

- Classify documents into 22+ predefined categories
- Batch processing of entire directories
- Support for PDF, images, Office documents, and text files
- Confidence scoring for classification results
- Support for multiple VLMs: MiniCPM, Qwen 3, Gemma 3, Ministral
- Unknown category detection

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5–12 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter a file path or directory path
4. View classification results with confidence scores

## Commands

| Command | Description |
|---------|-------------|
| `help` | Show available commands |
| `categories` | List all supported categories |
| `clear` | Clear the console |
| `exit` | Exit the application |

## Supported Categories

- Invoice, Receipt, Purchase Order, Check
- Passport, Driver License, ID Card
- Bank Statement, Tax Form, Utility Bill, Pay Stub
- Contract, Resume, Letter
- Medical Record, Insurance Claim
- Shipping Label, Business Card
- Birth Certificate, Marriage Certificate
- Loan Application, Company Registration

## Supported Formats

- Images: PNG, JPG, JPEG, BMP, GIF, TIFF, WebP, TGA, PSD
- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Text: TXT, HTML

## Example Output

```
┌──────────────────────────────────────────────────┐
│ Category:   invoice                              │
│ Confidence: 94%                                  │
│ Time:       234 ms                               │
└──────────────────────────────────────────────────┘
```

## Batch Processing

Enter a folder path to classify all supported documents:

```
Processing 5 document(s)...
──────────────────────────────────────────────────────────────────────
  ✓ invoice_2024.pdf                  invoice              94%  (1,234 ms)
  ✓ drivers_license.jpg               driver_license       89%  (987 ms)
  ✓ bank_statement.pdf                bank_statement       92%  (1,102 ms)
──────────────────────────────────────────────────────────────────────
Batch complete: 5/5 processed in 5,432 ms
```