# Smart Redaction (PII Detection + PDF Redaction)

Detect personally identifiable information (PII) in a PDF or image with an on-device model, let a
human review the suggestions, then permanently remove the approved values from the document.
Everything runs locally with LM-Kit.NET: no cloud, no data leaves the machine.

This sample combines two LM-Kit.NET capabilities into one compliance-ready workflow:

- **`LMKit.TextAnalysis.PiiExtraction`** finds sensitive values and their **bounding boxes**.
- **`LMKit.Document.Pdf.PdfRedactor`** deletes the underlying content (not a black box over it).

## Pipeline

```
IDENTIFY  ->  REVIEW (human-in-the-loop)  ->  REMOVE  ->  VERIFY
PiiExtraction   keep / drop each item        PdfRedactor    PdfSearch
```

1. **Identify** every PII item across all pages. Each detected entity carries a type, a confidence
   score, and its **occurrences**: the exact bounding boxes on the page (`TextRegion`).
2. **Review** the suggestions. A human keeps or drops each one, so accountability stays with a
   person, not the model. The default choice is to redact (secure by default).
3. **Remove** the approved values. Each occurrence's bounding box is turned into a redaction area
   with `PdfRedactionArea.FromRegion`, and the value is also removed everywhere it appears in text
   as a safety net.
4. **Verify** the output with a fresh, cache-free search, proving the approved values are gone.

## Input

- **PDF** files are redacted directly.
- **Image** files (png, jpg, tiff, bmp, webp, gif) are first converted to a searchable PDF with an
  OCR text layer, so the detector gets bounding boxes and the redactor can scrub the pixels.
- A bundled sample (`examples/account_application.pdf`, fictitious PII) is offered as option `0`.

## Features

- On-device PII detection with confidence scoring and page-accurate bounding boxes
- Human-in-the-loop review (per item, or bulk approve/keep)
- Bounding-box redaction: each mark hugs the matched glyphs, plus a value-based safety net
- True redaction: text glyphs, image pixels, vector graphics, and annotations are deleted, not covered
- Post-redaction verification that the removed values are unrecoverable
- Accepts PDFs and images; loops so you can process several documents in one session

## Prerequisites

- .NET 8.0 SDK or later
- A model for detection. The default is `qwen3.5:4b` (about 3.5 GB VRAM, or CPU). It downloads
  automatically on first run. Larger or vision models are offered for higher accuracy and scans.

## How It Works

The engine is configured with `PreferredInferenceModality = InferenceModality.Text`, which resolves
every detected entity to its exact glyph positions. Those positions (`PiiExtractedEntity.Occurrences`,
each a `TextRegion` with a bounding box) feed straight into `PdfRedactionArea.FromRegion(...)`, so
each mark is placed precisely. An OCR engine is attached so scanned pages also produce bounding boxes.

```csharp
var request = new PdfRedactionRequest();
foreach (var entity in approved)
{
    foreach (var occurrence in entity.Occurrences)          // occurrence = TextRegion (bounding box)
        request.Areas.AddRange(PdfRedactionArea.FromRegion(occurrence));

    request.SearchTerms.Add(entity.Value);                  // safety net
}

PdfRedactionResult result = PdfRedactor.RedactToBytes(File.ReadAllBytes(pdfPath), request);
```

## Usage

```bash
cd demos/console_net/document-intelligence/smart-redaction/smart_pii_redaction
dotnet run -c Release
```

1. Pick a model (or press Enter for the recommended default).
2. Pick the bundled sample (press Enter) or enter a path to your own PDF or image.
3. Review each detected item: `Enter`/`y` to redact, `n` to keep, `a` to redact all remaining,
   `k` to keep all remaining.
4. The redacted PDF is written next to the input as `<name>_redacted.pdf` and opened for review.

## Example Output

```
Detected 8 PII item(s) in 4.2s (overall confidence 0.94).

Review detected PII (human-in-the-loop):
  [Enter]/y = redact    n = keep    a = redact all remaining    k = keep all remaining

  [1/8] Person  "Jane A. Doe"  (confidence 0.98, 2 occurrence(s), page 1)   redact? [Y/n/a/k] a
  ...
Approved 8 of 8 item(s) for redaction.

Redacted 14 bounding box(es): 99 glyphs, 0 image region(s), and 0 annotation(s) across 1 page(s).
Saved: .../account_application_redacted.pdf

Verifying the redacted output (fresh, cache-free search)...
  All approved values are unrecoverable by text extraction.
```

## Configuration

Edit `Configuration.cs` to:

- Change the OCR engine, or remove it if you only process digital PDFs.
- Add custom PII categories (`extractor.PiiEntityDefinitions.Add(new PiiExtraction.PiiEntityDefinition("SWIFT code"));`).
- Provide domain `Guidance` to raise accuracy on specialized documents.

For batch processing, wrap `ProcessDocument` in a loop over a folder and apply a confidence policy
(auto-approve items above a threshold, queue the rest for a reviewer) to scale the workflow.
