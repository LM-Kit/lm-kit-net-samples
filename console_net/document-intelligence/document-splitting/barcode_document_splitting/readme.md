# Barcode Document Splitting

Split a scanned batch (PDF, multi-page TIFF or image) into separate documents by the barcodes printed on its pages, then write one file per document. The split is deterministic: barcodes are read by the native barcode reader built into LM-Kit.NET, so **no model is downloaded or loaded** and there is no model selection menu.

## Features

- Five splitting modes through `BarcodeSplittingOptions.Mode`:
  - `SeparatorPage`: a barcode sheet sits between two documents and is removed from the output
  - `FirstPageOfDocument`: the barcode page opens a document and stays in it
  - `LastPageOfDocument`: the barcode page closes a document and stays in it
  - `ValueChange`: a new document starts when the barcode value changes
  - `GroupByValue`: all pages carrying the same value form one document, even when not consecutive
- Optional value pattern (`ValuePattern`): a regular expression whose `value` group becomes the document's value
- Duplex scans handled: the blank back of a removed separator sheet is removed too (`RemoveBlankPageAfterSeparator`)
- Output files named from the barcode value with `FileNameTemplate = "{value}_{index:000}"`
- Full report: every page with its role and barcodes, every document with its pages and file, separator, discarded and failed pages, elapsed time
- Built-in sample batch: press Enter to generate a multi-page TIFF with QR separator sheets (encoded with `LMKit.Graphics.QrCode`)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- No GPU and no VRAM needed: the demo runs entirely on the CPU and loads no model

## How It Works

1. `BarcodeSplitting` scans every page of the batch and reads its barcodes.
2. `BarcodeSplittingOptions.Match` decides which barcodes count (all of them by default).
3. The mode turns matching barcodes into document boundaries; each page gets a `BarcodePageRole` (`Content`, `Separator`, `KeptSeparator`, `BlankDiscarded`, `LeadingDiscarded`).
4. `SplitToFiles` writes each document (`BarcodeSegment`) to the output folder as a PDF. PDF sources keep their pages as they are; image sources become image pages.

```csharp
using LMKit.Extraction.Barcodes;

var options = new BarcodeSplittingOptions
{
    Mode = BarcodeSplitMode.SeparatorPage,
    RemoveBlankPageAfterSeparator = true,
    FileNameTemplate = "{value}_{index:000}"
};

var splitter = new BarcodeSplitting(options);
BarcodeSplittingResult result = splitter.SplitToFiles("scanned_batch.pdf", "output");

foreach (BarcodeSegment segment in result.Segments)
{
    Console.WriteLine($"{segment.Label}: pages {segment.StartPage}-{segment.EndPage} -> {segment.OutputPath}");
}
```

## Usage

1. Run the application (`dotnet run`).
2. Enter the path of a PDF, TIFF or image batch, or press Enter to generate the sample batch in a temporary folder.
3. Choose what a barcode means (0 to 4). Enter keeps `SeparatorPage`.
4. Optionally enter a value pattern, for example `^(?<value>[A-Z]+)-`. Enter keeps the full barcode text.
5. Read the report. The documents are written to `<batch name>_split` next to the batch.
6. Press Enter to split another batch, or type `q` to quit.

## Example Output

Sample batch, `SeparatorPage` mode, no value pattern:

```
---------- Pages ----------
  Page   1  Separator                 [QR Code: INV-1001]
  Page   2  BlankDiscarded           (blank)
  Page   3  Content          doc 1
  Page   4  Content          doc 1
  Page   5  Separator                 [QR Code: PO-2001]
  Page   6  BlankDiscarded           (blank)
  Page   7  Content          doc 2
  ...
  Page  13  Separator                 [QR Code: INV-1001]
  Page  14  BlankDiscarded           (blank)
  Page  15  Content          doc 4

---------- Documents ----------
    1. INV-1001             pages 3, 4  -> INV-1001_001.pdf
    2. PO-2001              pages 7  -> PO-2001_002.pdf
    3. INV-1002             pages 10, 11, 12  -> INV-1002_003.pdf
    4. INV-1001             pages 15  -> INV-1001_004.pdf

---------- Summary ----------
  source           : scanned_batch.tif
  pages            : 15
  documents        : 4
  separator pages  : 1, 5, 8, 13
  discarded pages  : 1, 2, 5, 6, 8, 9, 13, 14
  failed pages     : none
  elapsed time     : 323 ms
```

Same batch, `GroupByValue` mode with the pattern `^(?<value>[A-Z]+)-`: two documents, `INV_001.pdf` (12 pages, both invoices and the addendum) and `PO_002.pdf` (3 pages).

## Configuration

| Option | Demo value | Purpose |
|---|---|---|
| `Mode` | chosen at runtime | What a matching barcode means |
| `ValuePattern` | entered at runtime | Extracts the document value from the barcode text |
| `RemoveBlankPageAfterSeparator` | `true` | Drops the blank back of a separator sheet in duplex scans |
| `FileNameTemplate` | `{value}_{index:000}` | Output names; tokens `{source}`, `{index}`, `{value}`, `{start}`, `{end}`, `{pages}`, `{format}` |
| `SeparatorHandling` | `Remove` (default) | Remove the separator sheet, or keep it as the first or last page |
| `LeadingPages` | `KeepAsDocument` (default) | What happens to pages before the first barcode |
| `Match` | all barcodes (default) | Restrict by format, exact values, pattern, search area, minimum size |
| `OutputFormat` | `Pdf` (default) | `Pdf`, `Tiff` or `SameAsSource` |

## Key Classes

- **`BarcodeSplitting`** (`LMKit.Extraction.Barcodes`): splits a batch by barcodes (`Split`, `SplitToAttachments`, `SplitToFiles`, `SplitToStreams`).
- **`BarcodeSplittingOptions`**: mode, match rule, value pattern, blank page handling, output naming and format.
- **`BarcodeSplittingResult`**: `Segments`, `Pages`, `SeparatorPages`, `DiscardedPages`, `FailedPages`, `Files`, `Elapsed`.
- **`BarcodeSegment`** / **`BarcodePage`**: one output document, and one scanned page with its role and barcodes.
