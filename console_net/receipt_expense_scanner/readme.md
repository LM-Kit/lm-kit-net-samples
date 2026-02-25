# AI Receipt & Expense Scanner

A demo that extracts structured expense data from receipts using **LM-Kit.NET's TextExtraction API**. Feed it any receipt and get back structured data: store info, line items, totals, tax, payment method, and automatic expense categorization.

## Features

- **Complete Receipt Parsing**: Store name, address, date, time, items, totals, tax, payment method
- **Line Item Extraction**: Each purchased item extracted with description, quantity, unit price, and total
- **Automatic Categorization**: AI suggests an expense category (Meals, Office Supplies, Travel, etc.)
- **Multiple Formats**: Parse `.txt` files directly or `.pdf`/`.png`/`.jpg` via document attachment
- **Built-In Sample**: Type `sample` to instantly see extraction with a realistic grocery receipt
- **JSON Output**: Get machine-readable JSON for accounting system integration
- **Fully Local**: All processing on your hardware, financial data never leaves your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (6-18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema designed for receipt data. The extraction engine parses receipt content and populates store info, line items, totals, and payment details.

1. Define extraction schema (store, items, totals, tax, payment)
2. Feed receipt content (text string or document/image attachment)
3. Call `Parse()` to extract structured expense data
4. Display results and JSON output

## Usage

1. Run the application
2. Select a model from the menu
3. Type `sample` for the built-in receipt or enter a file path
4. View the extracted expense data and JSON output
5. Type `q` to quit

## Example Output

```
╔═══════════════════════════════════════════════════════════════╗
║                    EXPENSE REPORT                             ║
╚═══════════════════════════════════════════════════════════════╝

  Store Name: Whole Foods Market
  Store Address: 399 4th Street, San Francisco, CA 94107
  Date: 01/15/2025
  Time: 12:47 PM
  Items: [10 items with description, quantity, unit price, total]
  Subtotal: 65.38
  Tax Rate: 8.625%
  Tax Amount: 5.08
  Discount: 6.54
  Total: 63.92
  Payment Method: VISA ending in 4821
  Transaction ID: WFM-SF-20250115-003847
  Expense Category: Groceries
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

## Tip: Scanning Receipt Images

For receipt images (photos, scans), use a vision-capable model or combine with OCR. See the `invoice_data_extraction` demo for image-based extraction with vision models and Tesseract OCR.
