# Structured Data Extraction

A demo for extracting structured data from text documents using LM-Kit.NET. Define custom schemas and extract fields into JSON format.

## Features

- Extract structured data from unstructured text
- Customizable extraction schemas with nested elements
- Support for arrays and complex object hierarchies
- Multiple data types: string, integer, float, date
- JSON output for easy integration
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (0.8–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Choose a sample document (invoice, job offer, medical record)
4. View extracted fields and JSON output

## Sample Schemas

### Invoice Extraction
- Invoice reference, dates, vendor/customer details
- Line items with quantities and prices
- Payment information (IBAN, BIC)
- Totals and VAT calculations

### Job Offer Extraction
- Position, salary, location, start date
- Company information
- Job description and employment terms

### Medical Record Extraction
- Patient information and demographics
- Medical history with conditions and treatments
- Vital signs, medications, allergies
- Lab results with reference ranges

## Defining Custom Elements

```csharp
var elements = new List<TextExtractionElement>
{
    new("Invoice Reference", ElementType.String, "Unique identifier"),
    new("Date", ElementType.Date, "Invoice date"),
    new("Items", new List<TextExtractionElement>
    {
        new("Description", ElementType.String),
        new("Quantity", ElementType.Integer),
        new("Unit Price", ElementType.Float)
    }, isArray: true)
};

textExtraction.Elements = elements;
```

## Supported Element Types

| Type | Description |
|------|-------------|
| String | Text values |
| Integer | Whole numbers |
| Float | Decimal numbers |
| Date | Date values |