# Text-to-SQL Fine-Tuning (C# .NET)

Teach a small local model the schema of YOUR database so it writes correct SQL without the schema pasted into every prompt. The base model invents table and column names (`order_date`, `stock_quantity`, `customer_name`); after fine-tuning on a small dataset file it emits schema-correct SQLite for questions it never saw. The demo ends by merging the adapter into one deployable GGUF model.

## What it does

1. Loads `qwen3.5:0.8b` and measures the base model on ten held-out questions: is the reply bare SQL, does it reference the right tables, does it use only identifiers that exist in the schema?
2. Loads `data/nl2sql.jsonl`, a 68-conversation chat JSONL dataset (the OpenAI fine-tuning shape) mapping natural-language questions to SQLite over a fictional bike-shop schema. The file format is auto-detected; ShareGPT, Alpaca, plain text, and ZIP archives load the same way.
3. Fine-tunes with LoRA: rank 16, attention + feed-forward target modules (the schema is new knowledge, not just style), cosine schedule with warmup, a 10 percent validation split, and sequence packing for throughput on short samples.
4. Re-measures the same held-out questions, then merges the adapter into a standalone model with `LoraMerger` (re-quantized back to the base precision) and proves the artifact answers on its own.

## Why it matters

- **Prompt-free schema knowledge.** Without fine-tuning, every request must carry the schema in the prompt: tokens, latency, and context spent on every single call. The fine-tune pays that cost once, at training time.
- **Dataset files, not data plumbing.** Training data loads from the formats teams already have (`AddDatasetFile`), with sample statistics and chat-template mask checking (`UnmaskedSampleCount`) before any compute is spent.
- **One deployable file.** The merged GGUF loads like any other model: no adapter management in production, no separate artifacts to version.
- Everything runs locally: the schema and the questions never leave the machine. Training is GPU-accelerated on CUDA and falls back to CPU.

## Prerequisites

- .NET 8.0 or later
- First run downloads `qwen3.5:0.8b` (about 600 MB)
- A CUDA GPU makes training more than an order of magnitude faster; CPU works

## Run

```bash
dotnet run -c Release
```

## Example output

```
Before fine-tuning:
  [ok ] SELECT COUNT(*) FROM customers WHERE city = 'Rome';
  [err] SELECT * FROM VeloShop WHERE Category = 'Road Bike' AND Price < 800;
  [err] SELECT * FROM orders ORDER BY order_date DESC LIMIT 3;
  [err] SELECT * FROM products WHERE stock_quantity < 3;
  ...
BASE: schema-correct SQL 2/10

Dataset: 68 conversations, 47 to 88 tokens per sample.
Training (rank 16, 8 epochs, attention + feed-forward)...
  epoch 8/8  validation loss 0.2588
Adapter saved: veloshop-sql.gguf (24972 KB)

After fine-tuning:
  [ok ] SELECT COUNT(*) FROM customers WHERE city = 'Rome';
  [ok ] SELECT * FROM products WHERE category = 'road' AND price_cents < 80000;
  [ok ] SELECT SUM(total_cents) FROM orders WHERE status = 'shipped';
  ...
TUNED: schema-correct SQL 8/10

Merging the adapter into a standalone model...
Merged model: veloshop-sql-merged.gguf (442 MB)
Merged model answers "How many orders are pending?":
  SELECT COUNT(*) FROM orders WHERE status = 'pending';

Held-out schema-correct SQL: 2/10 -> 8/10
```

## Key API

- `LoraFinetuning.AddDatasetFile(path)`: loads chat JSONL, ShareGPT, Alpaca, plain text, or ZIP archives; format auto-detected.
- `LoraTrainingParameters`: `Rank`, `Alpha`, `TargetModules` (`AttentionAndFeedForward` for knowledge tasks), `Epochs`, `LearningRate`, `Schedule`, `WarmupRatio`, `ValidationSplit`, `SequencePacking`.
- `SampleCount`, `SampleMinLength`, `SampleMaxLength`, `UnmaskedSampleCount`: dataset diagnostics before training.
- `FinetuningProgress`: live training loss, validation loss, and learning rate.
- `TrainToAdapter(path)` then `LM.ApplyLoraAdapter(source)`: train and hot-apply.
- `LoraMerger` with `EnableQuantization`: merge into a standalone GGUF at the base precision.

## Extend it

- Replace `data/nl2sql.jsonl` with question-to-SQL pairs over your real schema; keep the system prompt identical between training and inference.
- Widen coverage per question family (paraphrases matter more than raw count).
- Raise `Epochs` or `Rank` if held-out quality plateaus; watch the validation loss for overfitting.
- Skip the merge and ship the 25 MB adapter instead when one base model serves several tasks.
