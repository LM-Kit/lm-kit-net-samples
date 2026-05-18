# Encrypted PDF Workflows

Interactive console app that demonstrates the full LM-Kit.NET PDF toolkit on a password-protected PDF. Inspect, render, search, split, or edit, all without ever exporting an unlocked copy to disk.

## What it shows

- `PdfInfo.GetPageCount`, `GetMetadata`, `GetSecurityHandlerRevision` with `password` argument.
- `PdfRenderer.SavePageAsPngAsync` via `PdfRenderOptions.Password`.
- `PdfSearch.FindTextAsync` with `password` argument.
- `PdfSplitter.ExtractPagesAsync` with `password` argument.
- `PdfEditor` with a password-aware `Attachment` (`new Attachment(path, name, password)`).
- Async + `CancellationToken` throughout (Ctrl-C aborts cleanly).

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/encrypted_pdf_workflows
dotnet run
```

No command-line arguments. Pick one of six menu options:

| Mode | What it does |
|---|---|
| **Inspect** | Read metadata and security handler revision |
| **Render** | Render first page to a PNG |
| **Search** | Layout-aware text search |
| **Extract** | Split a page range to a new PDF |
| **Edit** | Keep every-other page via `PdfEditor` |
| **All** | Run all 5 steps end-to-end |

If you don't have an encrypted PDF, any editor (Adobe Acrobat, Foxit, an online tool) can add password protection to a normal PDF.

## Where this fits

Legal, finance, and healthcare pipelines routinely ingest password-protected PDFs. With the password support across the PDF toolkit, the same pipeline that works on open documents now works on encrypted ones unchanged. The password rides along through the API; no unlocked copy ever touches disk.
