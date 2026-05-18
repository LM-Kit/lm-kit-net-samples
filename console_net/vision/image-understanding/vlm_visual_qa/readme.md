# VLM Visual Q&A

Interactive console app that asks vision-language models analytical questions about images, on-device. Chat with a single image, run a standard 4-question audit, or caption a whole folder.

## What it shows

- `LM.LoadFromModelID("qwen3.5:4b" | "qwen3.5:9b" | "gemma4:e2b" | "gemma4:e4b" | "glm-4.6v-flash")` (current catalog).
- `LM.HasVision` for capability check at runtime.
- `LMKit.Data.Attachment(imagePath)` to attach an image to a turn.
- `chat.Submit(new ChatHistory.Message(prompt, attachment))` for vision input.
- `AfterTextCompletion` token streaming.
- Three interactive modes from a menu:
  - **Chat**: ask repeated questions about one image (REPL).
  - **Audit**: standard 4-question audit (caption, description, count, outdoors?).
  - **Folder**: caption every image in a folder, write a CSV.

## Run

```bash
cd console_net/vision/image-understanding/vlm_visual_qa
dotnet run
```

No command-line arguments. Pick the vision model at startup, then the mode from the menu.

## Where this fits

VLMs replace half a dozen task-specific computer-vision pipelines for unstructured images: captioning, counting, scene reasoning, document classification. One model, one API call, broad coverage. The folder mode produces a portable CSV that downstream tooling can consume immediately.
