# Multi-Document Entity Registry

Interactive console app that recognises named entities across a folder of documents and builds a deduped, cross-document registry. Built on LM-Kit.NET's `NamedEntityRecognition` engine for compliance, due-diligence, and discovery workflows.

## What it shows

- `NamedEntityRecognition.Recognize(attachment | text)` returning typed entities (`Person`, `Organization`, `Location`, `Date`, `Money`, ...) with a confidence score.
- Three interactive modes, picked from a menu:
  - **Live**: paste a paragraph, see entities immediately.
  - **File**: recognise entities in a single document (PDF / DOCX / TXT / MD / EML / PNG / JPG / ...).
  - **Folder**: walk a folder (optionally recursive), run NER per document with a min-confidence filter, then build:
    - a deduped **registry** of `(label, normalised value)` → occurrence count, distinct documents, max confidence,
    - a per-occurrence audit trail (every detection with its source document).
- Top-N table per entity label in the console (most cross-document first), plus two CSV outputs.

## Run

```bash
cd console_net/text-analysis/named-entity-recognition/ner_entity_extractor
dotnet run
```

The demo prompts for the model choice (Qwen 3.5 2B by default for speed), then for the mode and inputs. No command-line arguments.

## Where this fits

Compliance and discovery teams don't want a per-document entity dump — they want a *registry*: which entities recur across the corpus, where each occurrence came from, and how confident the model was. Folder mode produces exactly that, with the proper dedup ("Acme Corp" / "Acme Corp." / "ACME CORP" collapse to one row) and a full per-document audit trail.
