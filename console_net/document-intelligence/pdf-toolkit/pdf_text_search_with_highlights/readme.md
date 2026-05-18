# PDF Text Search with Highlights

Interactive console app that runs layout-aware keyword search across PDFs. Each match comes back with page index, snippet, and bounding-box coordinates ready for highlighting in a reviewer UI.

## What it shows

- `PdfSearch.FindTextAsync(input, query, pageRange, textOptions, password, ct)` returning `PdfTextSearchResult`.
- `TextSearchOptions { Comparison, WholeWord, MaxResults, ContextChars }`.
- `PdfTextSearchResult { ScannedPages, PageCount, TotalMatches, LimitedByMaxMatches, Matches }`.
- `TextMatch { PageIndex, Bounds, Snippet, Text }` with positional bounds for highlighting.
- Two interactive modes from a menu:
  - **Search**: open one PDF (password optional), then run repeated queries (REPL). Optional CSV export per query.
  - **Folder**: run one term across every PDF in a folder and write a single audit CSV.

## Run

```bash
cd console_net/document-intelligence/pdf-toolkit/pdf_text_search_with_highlights
dotnet run
```

No command-line arguments. Pick the mode from the menu and follow the prompts.

## Where this fits

Reviewer UIs and audit pipelines need both the snippet AND the rectangle, so they can draw a highlight on the rendered page. Compliance teams also need the folder-wide scan: "where does the word `indemnification` appear across our contract archive?". This demo wraps both behind the same SDK call.
