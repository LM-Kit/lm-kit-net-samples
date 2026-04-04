# AI Receipt & Expense Scanner

A demo that extracts structured expense data from receipts using **LM-Kit.NET's TextExtraction API** with **vision-language models**. Feed it any receipt (text, PDF, or scanned photo) and get back structured data: store info, line items, totals, tax, payment method, and automatic expense categorization.

## Features

- **Vision-Language Models**: Uses VLM models to process both text and image-based receipts
- **Complete Receipt Parsing**: Store name, address, date, time, items, totals, tax, payment method
- **Line Item Extraction**: Each purchased item extracted with description, quantity, unit price, and total
- **Automatic Categorization**: AI suggests an expense category (Meals, Office Supplies, Travel, etc.)
- **Multiple Formats**: Parse `.txt` files directly or `.pdf`/`.png`/`.jpg`/`.bmp`/`.tiff` via document attachment
- **Built-In Sample**: Type `sample` to instantly see extraction with a realistic grocery receipt
- **JSON Output**: Get machine-readable JSON for accounting system integration
- **Fully Local**: All processing on your hardware, financial data never leaves your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5 to 18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema designed for receipt data and a vision-language model. The VLM can process both text documents and scanned receipt photos, extracting store info, line items, totals, and payment details from any format.

1. Select a vision-language model from the menu
2. Define extraction schema (store, items, totals, tax, payment)
3. Feed receipt content (text string or document/image attachment)
4. Call `Parse()` to extract structured expense data
5. Display results and JSON output

## Usage

1. Run the application
2. Select a VLM model from the menu
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
| 0 | Z.ai GLM-V 4.6 Flash 10B | ~7 GB |
| 1 | MiniCPM o 4.5 9B | ~5.9 GB |
| 2 | Alibaba Qwen 3.5 2B | ~2 GB |
| 3 | Alibaba Qwen 3.5 4B | ~3.5 GB |
| 4 | Alibaba Qwen 3.5 9B (Recommended) | ~7 GB |
| 5 | Google Gemma 4 E4B | ~6 GB |
| 7 | Alibaba Qwen 3.5 27B | ~18 GB |
| 8 | Mistral Ministral 3 8B | ~6.5 GB |
