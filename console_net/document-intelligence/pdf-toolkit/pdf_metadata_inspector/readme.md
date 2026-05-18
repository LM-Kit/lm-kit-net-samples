# PDF Metadata Inspector

Read-only inspection pass for any PDF: standard metadata fields, XMP block, security flags, document permissions, and per-page dimensions.

## What it shows

- `PdfInfo.GetMetadata(path)` returns a `PdfMetadata` with `Title`, `Author`, `Subject`, `Keywords`, `Creator`, `Producer`, `CreationDate`, `ModDate`, `PageCount`, `FileVersion`, `XmpMetadata`.
- `PdfInfo.GetSecurityHandlerRevision(path)` returns 0 if the file is not encrypted, > 0 otherwise.
- `PdfInfo.GetPermissions(path)` returns a `DocumentPermissions` flags value (`Print`, `Copy`, `Modify`, `Annotate`, ...).
- `PdfInfo.GetPageInfo(path, pageIndex)` returns a `PdfPageInfo` with `Width`, `Height` (PDF points), `Orientation`, `IsTextOnly`.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_metadata_inspector
dotnet run -- C:\docs\contract.pdf
```

## Where this fits

Every regulated workflow starts by classifying the PDF: is it encrypted, can we copy text out, what version are we dealing with, does it have an XMP block we must preserve. This is the smallest tool that answers those questions before any heavier ingestion path runs.
