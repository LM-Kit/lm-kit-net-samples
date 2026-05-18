# Folder PII Redaction Tool

Interactive console app that detects PII spans in text files and writes position-accurate redacted copies plus a full audit trail. Built on LM-Kit.NET's `PiiExtraction` engine.

## What it shows

- `PiiExtraction.Extract(attachment | text)` returning entities with `Occurrences[].StartIndex` / `.EndIndex` — position-accurate spans, not string-replace approximation.
- Overlap resolution: longest span wins, then highest confidence on ties.
- Three redaction modes, chosen at runtime:
  - **mask** — `"John Smith"` → `"**** *****"` (preserves shape).
  - **label** — `"John Smith"` → `"[PERSON]"`.
  - **hash** — `"John Smith"` → `"[PERSON#a1b2c3]"` (stable per value).
- Three interactive modes from a menu:
  - **Live**: paste text, see detected spans + redacted output.
  - **File**: redact one text file; companion `_audit.csv` next to the output.
  - **Folder**: redact every `.txt` / `.md` / `.eml` / `.log` preserving relative paths, produce `redaction_audit.csv` listing every redacted span.

## Run

```bash
cd console_net/text-analysis/pii-extraction/pii_detector_redactor
dotnet run
```

The demo prompts for the model, the redaction mode, the input path, and the output path. No command-line arguments.

## Where this fits

PII redaction is a position problem, not a search-and-replace problem. The same string can be a name in one paragraph and an irrelevant noun in another; case-insensitive replace mangles both. By using `Occurrences[].StartIndex` / `.EndIndex` directly, this demo redacts only what the model flagged, exactly where it flagged it, with a complete audit trail (label, original text, redaction, byte offsets, confidence).
