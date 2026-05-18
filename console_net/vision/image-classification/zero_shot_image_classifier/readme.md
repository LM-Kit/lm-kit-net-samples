# Photo Auto-Sorter (Zero-Shot)

Interactive console app that classifies images into a caller-defined category list, then copies or moves each image into the matching per-category folder. Built on LM-Kit.NET's `Categorization` engine — a deterministic classifier with `Confidence`, not a free-form VLM prompt.

## What it shows

- `Categorization.GetBestCategory(IList<string> categories, ImageBuffer image)` returning the index of the best match, plus `.Confidence`.
- Two interactive modes from a menu:
  - **Live**: pick categories, then type image paths one by one, see the verdict.
  - **Folder**: pick categories, point at an input folder, choose copy/move + a confidence floor, end up with a sorted folder tree.
- Three ways to supply the category list:
  - **Default**: 10 sensible buckets (product photo, screenshot, document scan, …).
  - **Custom**: type one label per line in the prompt.
  - **File**: path to a categories file (one label per line, `#` for comments).
- Confidence below the threshold is routed to `_uncertain/` so a reviewer can handle the doubtful cases.
- `sort_manifest.csv` lists every source path, chosen category, confidence, and destination path for full audit.

## Run

```bash
cd console_net/vision/image-classification/zero_shot_image_classifier
dotnet run
```

Pick a vision model, pick a mode, follow the prompts. No command-line arguments.

## Where this fits

VLMs *can* describe an image freely, but a triage / archive pipeline wants a deterministic verdict against a fixed taxonomy. `Categorization` does exactly that and returns a confidence the team can threshold on. This demo turns those per-image verdicts into an actual on-disk reorganisation — including an audit manifest and an `_uncertain/` bucket for the low-confidence cases.
