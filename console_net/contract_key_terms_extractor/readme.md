# AI Contract Key Terms Extractor

A demo that extracts key clauses and terms from contracts using **LM-Kit.NET's TextExtraction API**. Feed it any legal agreement and get back structured data: parties, dates, financials, termination terms, IP rights, confidentiality, liability, and risk flags.

## Features

- **Comprehensive Clause Extraction**: Contract type, parties, dates, value, payment terms, termination, confidentiality, IP, liability, governing law
- **Party Identification**: Each party extracted with name, role, and address
- **Risk Detection**: AI flags unusual or potentially risky clauses (non-compete, exclusivity, unlimited liability, auto-renewal)
- **Multiple Formats**: Parse `.txt` files directly or `.pdf`/`.docx` via document attachment
- **Built-In Sample**: Type `sample` to instantly see extraction with a realistic Master Services Agreement
- **JSON Output**: Get machine-readable JSON for contract management systems
- **Fully Local**: All processing on your hardware, confidential contracts never leave your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (6-18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema designed for legal contract analysis. The extraction engine parses contract text and populates key terms, clauses, and risk indicators.

1. Define extraction schema (parties, dates, terms, clauses, risks)
2. Feed contract content (text string or document attachment)
3. Call `Parse()` to extract key terms
4. Display results and JSON output

## Usage

1. Run the application
2. Select a model from the menu
3. Type `sample` for the built-in contract or enter a file path
4. View the extracted key terms and JSON output
5. Type `q` to quit

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
| 0 | Qwen-3 8B (Recommended) | ~6 GB |
| 1 | Gemma 3 12B | ~9 GB |
| 2 | Qwen-3 14B | ~10 GB |
| 3 | Phi-4 14.7B | ~11 GB |
| 4 | GPT OSS 20B | ~16 GB |
| 5 | GLM 4.7 Flash 30B | ~18 GB |
| 6 | Qwen-3.5 27B | ~18 GB |
