# Social-Media Sarcasm Triage

Interactive console app that flags suspected sarcasm in incoming messages using LM-Kit.NET's fine-tuned `lmkit-sarcasm-detection` classifier. Built for social-media, community-management, and CX teams who need a triage pass before sentiment dashboards or auto-replies.

## What it shows

- `SarcasmDetection.IsSarcastic(text)` returning a boolean verdict plus the engine's `Confidence` score.
- Three input modes, picked from an interactive menu:
  - **Live**: type one message at a time and see the verdict immediately.
  - **Sample**: run the built-in 12-message dataset to see the demo end-to-end.
  - **File**: classify every line of a UTF-8 text file (1 message per line).
- Per-batch console summary: total / sarcastic / sincere / median latency, top suspected items.
- Optional CSV export after a batch: `id, classified_at, is_sarcastic, confidence, latency_ms, text`.

## Run

```bash
cd console_net/text-analysis/sentiment-analysis/sarcasm_detection
dotnet run
```

The demo prompts for everything; there are no command-line arguments. Pick `1` to type messages, `2` to try the sample, `3` to load a file, `q` to quit.

## Where this fits

Sarcasm collapses sentiment dashboards: a sarcastic complaint reads as "positive" to keyword-based classifiers and "ambiguous" to general LLMs. The fine-tuned LM-Kit classifier produces a calibrated verdict and confidence, suitable for an automated triage queue that routes high-confidence sarcasm to a human reviewer.

## Language support

`lmkit-sarcasm-detection` is fine-tuned specifically for English. For other languages, fine-tune your own classifier on labelled data.
