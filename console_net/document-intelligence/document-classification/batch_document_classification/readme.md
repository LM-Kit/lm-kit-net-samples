# Batch Document Classification

A high-throughput demo for automatically classifying large document collections into predefined categories using LM-Kit.NET, with parallel processing and real-time performance metrics.

## Features

- **Batch processing** of entire directory trees with automatic subfolder traversal
- **Parallel classification** with configurable thread count
- **30 predefined categories** covering common document types (invoices, contracts, IDs, etc.)
- **Real-time statistics**: documents/sec, average processing time, confidence scores
- **Automatic file organization** into category-based output folders
- **Unknown category support** for documents that don't match predefined types

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for parallel inference (8+ GB recommended for multi-threaded processing)

## Configuration

### Model Selection

The demo uses the `lmkit-tasks:4b-preview` model by default:

```csharp
var model = new LM(
    ModelCard.GetPredefinedModelCardByModelID("lmkit-tasks:4b-preview").ModelUri);
```

### Categories

The following document categories are supported:

| Category | Category | Category |
|----------|----------|----------|
| bank_details | insurance_claim | receipt |
| bank_statement | insurance_policy | resume |
| birth_certificate | invoice | residence_permit |
| business_card | letter | shipping_document |
| check | loan_application | shipping_label |
| company_registration | marriage_certificate | tax_form |
| contract | medical_record | unknown |
| driver_license | national_id | utility_bill |
| id_card | passport | |
| | pay_stub | |
| | payment_card | |
| | payroll_statement | |
| | purchase_order | |

### Custom Categories

Modify the `Categories` list to add domain-specific types:

```csharp
private static readonly List<string> Categories = new()
{
    "bank_statement",
    "invoice",
    "contract",
    // Add your custom categories here
    "warranty_card",
    "membership_form"
};
```

## Usage

1. Run the application

2. Enter the input folder path when prompted

3. Enter the output folder path

4. Specify thread count (default: 1)

5. Monitor real-time progress:

```
[1/500] [T04] agreement.pdf -> contract (92%) [1250ms] (avg: 1250ms)
[2/500] [T04] electric_bill.pdf -> utility_bill (88%) [980ms] (avg: 1115ms)
[3/500] [T07] passport_scan.jpg -> passport (95%) [1100ms] (avg: 1110ms)
```

6. Retrieve organized files from the output directory

## Console Output

Each line displays:

| Field | Description |
|-------|-------------|
| **[n/total]** | Progress counter |
| **[Txx]** | Thread ID |
| **filename** | Source document name |
| **category** | Assigned classification |
| **confidence** | Model confidence score |
| **time** | Processing time for this document |
| **avg** | Running average time per document |

## Output Structure

Files are automatically organized into category folders:

```
Output/
├── contract/
│   ├── agreement.pdf
│   └── service_contract.docx
├── invoice/
│   ├── inv_001.pdf
│   └── inv_002.pdf
├── passport/
│   └── passport_scan.jpg
└── unknown/
    └── misc_document.pdf
```

Duplicate filenames are automatically handled with numeric suffixes.

## Supported Formats

- **Documents**: PDF, DOCX, XLSX, PPTX, EML, MBOX
- **Images**: PNG, JPG, JPEG, TIFF, BMP, WebP, GIF, TGA, PSD, PNM, HDR, PIC
- **Text**: TXT, HTML

## Performance

At completion, view aggregate statistics:

```
=== Complete ===
Processed: 500/500 in 425.3s
Speed: 1.18 docs/sec
Avg confidence: 89%
Avg time/doc: 850ms
Output: D:\Output
```

## Use Cases

- **Document management**: Automatically sort incoming documents into organized folders
- **Compliance preparation**: Classify document repositories for audit readiness
- **Data migration**: Organize legacy file shares before cloud migration
- **Mailroom automation**: Route scanned documents to appropriate departments
- **Archive organization**: Structure historical document collections

## Thread Recommendations

| VRAM | Recommended Threads |
|------|---------------------|
| 4 GB | 1 |
| 8 GB | 1–2 |
| 12+ GB | 2–4 |