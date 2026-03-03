# AI Contract Key Terms Extractor

A demo that extracts key clauses and terms from contracts using **LM-Kit.NET's TextExtraction API** with **vision-language models (VLMs)**. Feed it any legal agreement, whether plain text, PDF, DOCX, or a scanned contract image, and get back structured data: parties, dates, financials, termination terms, IP rights, confidentiality, liability, and risk flags.

## Features

- **Vision-Language Models**: Process scanned contract images, photographed documents, and standard text formats using VLMs
- **Comprehensive Clause Extraction**: Contract type, parties, dates, value, payment terms, termination, confidentiality, IP, liability, governing law
- **Party Identification**: Each party extracted with name, role, and address
- **Risk Detection**: AI flags unusual or potentially risky clauses (non-compete, exclusivity, unlimited liability, auto-renewal)
- **Multiple Formats**: Parse `.txt` files directly, `.pdf`/`.docx` via document attachment, or scanned images (`.png`, `.jpg`, `.bmp`, `.tiff`, `.webp`)
- **Built-In Sample**: Type `sample` to instantly see extraction with a realistic Master Services Agreement
- **JSON Output**: Get machine-readable JSON for contract management systems
- **Fully Local**: All processing on your hardware, confidential contracts never leave your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5-18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema designed for legal contract analysis. Vision-language models enable direct processing of scanned contracts and photographs alongside traditional text-based documents.

1. Select a vision-language model
2. Define extraction schema (parties, dates, terms, clauses, risks)
3. Feed contract content (text string, document attachment, or scanned image)
4. Call `Parse()` to extract key terms
5. Display results and JSON output

## Usage

1. Run the application
2. Select a vision-language model from the menu
3. Type `sample` for the built-in contract or enter a file path
4. View the extracted key terms and JSON output
5. Type `q` to quit

## Supported Formats

- **Documents**: PDF, DOCX
- **Images**: PNG, JPG, JPEG, BMP, TIFF, WebP (scanned contracts, photographs)
- **Text**: TXT

## Example Output

```
╔═══════════════════════════════════════════════════════════════╗
║                   KEY CONTRACT TERMS                          ║
╚═══════════════════════════════════════════════════════════════╝

  Contract Type: Master Services Agreement
  Contract Title: Master Services Agreement
  Parties: [NovaTech Solutions Inc. (Provider), Meridian Global Partners LLC (Client)]
  Effective Date: March 1, 2025
  Expiration Date: February 28, 2027
  Contract Value: $1,080,000 USD (estimated)
  Payment Terms: Monthly retainer $45,000, Net 30, 1.5% late interest
  Termination Clause: 30 days cure for breach, 60 days convenience termination
  Confidentiality: Mutual, survives 5 years post-termination
  Liability Limitation: Capped at 12 months fees, excludes IP and confidentiality
  Intellectual Property: Work Product owned by Client, Provider IP licensed
  Governing Law: New York, arbitration via AAA
  Key Obligations: Software development, cloud consulting, technical support
  Renewal Terms: Auto-renews 12 months, 90 days notice to cancel
  Risk Flags: Auto-renewal, non-solicitation 12 months, late payment interest
```

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
