# LoRA Fine-Tuning: Product Identity (C# .NET)

Give the model YOUR product identity. The tuned model introduces itself as Atlas, the on-device assistant of the fictional Northwind Robotics, with no system prompt at inference time. The base model answers as a generic AI; after a training run of a few seconds it answers as your product. This is the smallest complete fine-tuning loop: data in code, live loss, a measurable before/after, a GGUF adapter out.

## What it does

1. Loads `qwen3.5:0.8b` and asks eight held-out identity questions with **no system prompt** ("I forgot your name, what was it?", "State your name and maker."). A correct reply names both Atlas and Northwind Robotics.
2. Fine-tunes with LoRA on twelve question phrasings mapped to the identity answer: rank 8 attention adapters, 3 epochs. Training takes seconds on a CUDA GPU.
3. Re-asks the same held-out questions, still with no system prompt, and reports identity adoption before and after. The adapter is saved as a GGUF file.

## Why it matters

- **An identity in the weights beats an identity in the prompt.** System prompts cost tokens on every request, can be overridden or leaked, and drift in long conversations. A fine-tuned identity is always there, at zero prompt cost.
- **White-label and embedded products need this.** An assistant shipped inside a device or product should answer as that product, not as the base model it was built from.
- **This is the on-ramp.** The same loop scales to real tasks: the [Text-to-SQL demo](../text_to_sql_fine_tuning) teaches a database schema from a dataset file, and the [Vision Display demo](../vision_display_fine_tuning) trains on labeled images.
- Everything runs locally, and the adapter is a 2 MB GGUF file that hot-applies onto the base model.

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
Before fine-tuning (no system prompt):
  [err] Hello! I'm Qwen3.5, a large language model developed by Tongyi Lab...
  [err] I was created by Alibaba Cloud...
BASE: identity adopted 0/8

Training on 12 samples (rank 8, 3 epochs)...
Adapter saved: atlas-identity.gguf (2115 KB)

After fine-tuning (still no system prompt):
  [ok ] I am Atlas, the on-device assistant of Northwind Robotics. I run enti...
  [ok ] I am Atlas, the on-device assistant of Northwind Robotics...
TUNED: identity adopted 8/8

Held-out identity adoption: 0/8 -> 8/8
```

## Key API

- `LoraFinetuning` with `Parameters`: `Rank`, `Alpha`, `TargetModules`, `Epochs`, `LearningRate`.
- `AddTrainingData(ChatHistory)`: conversations packed with `AuthorRole.BeginOfNewConversation`; assistant turns are supervised, user turns are masked (`AssistantLossOnly`).
- `UnmaskedSampleCount`: warns when samples cannot be masked to assistant-only loss.
- `FinetuningProgress`: live loss per optimizer step.
- `TrainToAdapter(path)` then `LM.ApplyLoraAdapter(source)`: train and hot-apply; `TrainToModel` emits a single merged GGUF instead.

## Extend it

- Swap the Atlas identity for your product's name, maker, and voice.
- Load examples from a JSONL file with `AddDatasetFile` instead of building them in code.
- Move to a real task next: schema-aware SQL from a dataset file, or display reading from labeled images (sibling demos in this folder).
- Ship one adapter per brand on the same base model; see the LoRA Adapter Hot-Swap demo.
