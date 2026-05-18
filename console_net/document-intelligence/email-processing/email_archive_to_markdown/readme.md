# Email Archive to Markdown

Interactive console app that converts `.eml` and `.mbox` files (headers, body, embedded attachments, scanned images) into searchable Markdown. Single file or whole archive folder.

## What it shows

- `DocumentToMarkdown` handles `.eml` and `.mbox` natively under the Hybrid strategy.
- `VlmOcr` is wired as the OCR engine for any scanned attachments inside emails.
- `DocumentToMarkdownResult.Markdown` carries the converted text; `Pages` carries per-page metadata.
- Two interactive modes from a menu:
  - **File**: convert one email file, optional preview.
  - **Archive**: convert every email in a folder (recursive), optional combined `archive.md` index.

## Run

```bash
cd console_net/document-intelligence/email-processing/email_archive_to_markdown
dotnet run
```

No command-line arguments. The OCR model loads once at startup. Pick the mode from the menu and follow the prompts.

## Where this fits

Compliance, eDiscovery, helpdesk analytics, and customer-service pipelines all start by normalizing email archives. Markdown out is the universal feed for everything downstream: vector search, RAG, classification, summarization, fine-tuning datasets.
