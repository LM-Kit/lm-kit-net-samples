# Customer Feedback Sentiment Dashboard

Interactive console app that classifies a batch of customer reviews, segments the results by product and source, and surfaces the strongest negatives for human follow-up. Built on LM-Kit.NET's fine-tuned `lmkit-sentiment-analysis` classifier.

## What it shows

- `SentimentAnalysis.GetSentimentCategory(text)` returning Positive / Negative (or Neutral with `NeutralSupport = true`) plus `.Confidence`.
- Three input modes from an interactive menu:
  - **Live**: classify one review at a time as you type.
  - **Sample**: run a built-in 12-review dataset to see the dashboard end-to-end.
  - **File**: ingest either a flat `.txt` (one review per line) or a structured `.csv` (`id,product,source,date,text`).
- Per-batch dashboard:
  - Overall pos / neu / neg counts and rates.
  - Per-product table sorted by negative rate (worst first).
  - Per-source table sorted by negative rate.
  - Top 5 strongest negatives for review.
- Optional CSV export after a batch:
  - `<name>_classified.csv` — one row per review with category + confidence.
  - `<name>_summary.csv` — one row per segment with totals and negative rate.

## Run

```bash
cd console_net/text-analysis/sentiment-analysis/customer_feedback_sentiment
dotnet run
```

The demo prompts for everything; no command-line arguments. Pick the input mode from the menu, see the dashboard, optionally save the CSVs.

## Where this fits

A weekly stream of hundreds of reviews is impossible to triage manually. The team needs a segmented view: which product has the worst negative rate this week, which channel surfaces the strongest complaints, who to follow up with first. The fine-tuned classifier gives a calibrated verdict per review; the dashboard turns those verdicts into something actionable.
