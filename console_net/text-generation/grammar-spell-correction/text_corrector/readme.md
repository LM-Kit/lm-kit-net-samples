# Folder Grammar Reviewer with Diff

Interactive console app that corrects grammar/spelling/punctuation in text content and produces a per-file diff plus an edit-ratio ranking. Built on LM-Kit.NET's `TextCorrection` engine for editorial, documentation, and content-ops teams.

## What it shows

- `TextCorrection.Correct(text, cancellationToken)` for a single string.
- Four interactive modes, picked from a menu:
  - **Live**: type or paste one line, get the corrected line back immediately.
  - **Sample**: run 5 built-in error-laden sentences end-to-end.
  - **File**: correct one `.txt` / `.md` file, write the corrected copy to a chosen path, optionally show the inline diff.
  - **Folder**: walk a folder of `.txt` / `.md` files, write corrected copies preserving the relative path, and produce:
    - one `diffs/<file>.diff.md` per source (a ` `/`+`/`-` line diff in a fenced `diff` block),
    - `review_report.md` summarising files by edit ratio,
    - `review_summary.csv` for downstream tooling.
- Top-N edit-ratio ranking in the console so the worst files get reviewed first.

## Run

```bash
cd console_net/text-generation/grammar-spell-correction/text_corrector
dotnet run
```

The demo prompts for the model choice (Qwen 3.5 9B by default), then for the mode. No command-line arguments.

## Where this fits

Editorial workflows don't want a black-box "fix it all" pass — they want to see what the model changed, on which files, and prioritise the worst-edited files for human review. The folder mode produces exactly that artefact set: corrected copies, per-file diffs, and a sortable summary.
