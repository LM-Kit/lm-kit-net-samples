# Vision Fine-Tuning: Seven-Segment Display Reader (C# .NET)

Teach a small vision-language model to read seven-segment displays. Meters, scales, and panel instruments show values on segment displays that generic models misread (dropped digits, confused segments); this demo renders labeled display images, fine-tunes with LoRA through the model's vision path, and measures reading accuracy on held-out values before and after. The same workflow applies to any labeled image set: replace the generated images with your own photos.

## What it does

1. Generates deterministic seven-segment display images (lit segments plus a faint ghost of the unlit ones, the look of a real LCD instrument) for disjoint train and held-out value sets. The renderer is plain BMP writing, no image library.
2. Measures the base `qwen3.5:0.8b` on ten held-out displays: does the reply contain the exact value?
3. Fine-tunes on 48 labeled displays: each training sample is a conversation whose user turn carries the image and whose assistant turn is the expected reading. Images are encoded through the frozen vision tower exactly as inference encodes them.
4. Re-measures the same held-out displays with the adapter applied.

## Why it matters

- **Reading instruments is a real edge workload.** Utilities, manufacturing, and lab equipment expose values only on physical displays. A small tuned VLM reads them on-device, without cloud calls or camera feeds leaving the site.
- **Vision fine-tuning uses the same API as text.** `AddTrainingData` with image attachments, the same `LoraTrainingParameters`, the same GGUF adapter output.
- **Image cost is a controlled budget.** `Configuration.DefaultImageDetail` decides the pixel budget per image, and so the vision-token cost of every training step.

## Prerequisites

- .NET 8.0 or later
- First run downloads `qwen3.5:0.8b` (about 600 MB, vision-capable)
- A CUDA GPU is recommended; image samples cost more per step than text

## Run

```bash
dotnet run -c Release
```

## Example output

```
Before fine-tuning:
  [err]  87.3 <- 8.3
  [err]  91.4 <- 8.1.4
  [ok ]  59.6 <- 59.6
  ...
BASE: displays read correctly 3/10

Training on 48 labeled displays (rank 8, 3 epochs)...
Adapter saved: display-reader.gguf (2115 KB)

After fine-tuning:
  [ok ]  70.7 <- The display reads 70.7.
  [ok ]  91.4 <- The display reads 91.4.
  [ok ]  59.6 <- The display reads 59.6.
  ...
TUNED: displays read correctly 7/10

Held-out displays read correctly: 3/10 -> 7/10
```

## Key API

- `Configuration.DefaultImageDetail`: the pixel budget every image gets before vision encoding; the lever for vision-token cost per sample.
- `ChatHistory.Message(question, attachment)` + `AddTrainingData`: labeled images as training conversations, packed with `AuthorRole.BeginOfNewConversation`.
- `LoraTargetModules.Attention`: image training adapts the attention projections (the `Output` module is not supported with images).
- `TrainToAdapter` then `LM.ApplyLoraAdapter`: a 2 MB adapter turns the base VLM into a display reader.

## Extend it

- Replace the generated BMPs with photos of your real displays and their readings; a `new Attachment(path)` per image is all that changes.
- Ship the dataset as a ShareGPT ZIP (JSON plus an images folder) and load it with `AddDatasetFile` instead of building conversations in code.
- Raise `ImageDetail` when fine detail carries the answer; raise `Rank` or epochs when the reading task is harder than digits.
