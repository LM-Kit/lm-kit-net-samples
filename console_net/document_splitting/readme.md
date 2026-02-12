# Document Splitting

Automatically detect logical document boundaries within multi-page PDFs using a vision language model, then optionally split them into separate PDF files.

## Features

- Vision-based analysis of each page to identify document types
- Automatic boundary detection between different documents
- Descriptive labels for each detected segment (e.g., "Invoice", "National ID Card", "Pay Slip")
- Page ranges and confidence scores for each segment
- **Physical PDF splitting**: export each detected segment as a separate PDF file using `PdfSplitter`
- Interactive destination directory selection
- Optional guidance to improve detection accuracy
- Interactive console loop for processing multiple files

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- A vision-capable model (8B or larger recommended for best accuracy, ~6.5 GB VRAM)

## Usage

1. Run the application
2. Select a vision model from the menu (Qwen 3 8B recommended)
3. Enter the path to a multi-page PDF file
4. View the detected document segments with page ranges and labels
5. If multiple documents are detected, choose whether to split the PDF into separate files
6. Select the output directory for the split files
7. Process additional files or type 'q' to quit

## Example Output

```
Analyzing 10 page(s)...

────────── Results ──────────
  Documents found     : 6
  Multiple documents  : True
  Confidence          : 85%

  ▶ Page 1 (1 page)  Post Office Registered Mail Envelope
  ▶ Pages 2-6 (5 pages)  MGAS Affiliation Form
  ▶ Page 7 (1 page)  LCL Bank Account Statement
  ▶ Page 8 (1 page)  French National Identity Card
  ▶ Page 9 (1 page)  Payroll Statement
  ▶ Page 10 (1 page)  Insurance Quote

────────── Stats ────────────
  elapsed time : 45.32 s
  total pages  : 10
  segments     : 6
────────────────────────────

Assistant — 6 documents were detected. Would you like to split them into separate PDF files? (y/n)
> y

Assistant — enter output directory (press Enter for 'C:\docs\scanned_batch_split'):
>

Splitting into 'C:\docs\scanned_batch_split'...

✔ Successfully created 6 file(s):
  ▶ scanned_batch_1.pdf  1 page  (Post Office Registered Mail Envelope)
  ▶ scanned_batch_2.pdf  5 pages  (MGAS Affiliation Form)
  ▶ scanned_batch_3.pdf  1 page  (LCL Bank Account Statement)
  ▶ scanned_batch_4.pdf  1 page  (French National Identity Card)
  ▶ scanned_batch_5.pdf  1 page  (Payroll Statement)
  ▶ scanned_batch_6.pdf  1 page  (Insurance Quote)
```

## Key Classes

- **`DocumentSplitting`** (`LMKit.Extraction`): uses a vision language model to detect document boundaries.
- **`PdfSplitter`** (`LMKit.Document`): physically splits a PDF into separate files based on detected segments.
- **`DocumentSplittingResult`** / **`DocumentSegment`**: result types with page ranges, labels, and confidence.
