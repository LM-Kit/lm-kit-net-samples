# Image Tag Index & Lookup

Interactive console app that tags a folder of images with a vision-language model (5–10 short tags each), builds an inverted index, and lets you look up images by tag. Built on grammar-constrained JSON output so the result is guaranteed-parseable — no regex.

## What it shows

- `MultiTurnConversation` with a vision model and a per-image instruction.
- `MultiTurnConversation.Grammar = new Grammar(Grammar.PredefinedGrammar.JsonStringArray)` to force the completion into a valid JSON array of strings.
- `System.Text.Json.JsonSerializer.Deserialize<string[]>` to read the result — no tolerant regex, no fallback parser.
- Four interactive modes from a menu:
  - **Live**: type one image path, get its tags.
  - **Index**: walk a folder, tag every image, build both `tags_index.json` (image → tags) and `tags_inverted.csv` (tag → count + sample images).
  - **Lookup**: REPL over the inverted index — type a tag, get matching images.
  - **Stats**: count of images / distinct tags / total tag uses + the top-15 tag frequency table.

## Run

```bash
cd console_net/vision/image-labeling/image_tag_generator
dotnet run
```

Pick a vision model, pick a mode, follow the prompts. No command-line arguments.

## Where this fits

E-commerce, DAM, social feeds, content moderation — all need fast tag coverage at upload time. Grammar-constrained decoding guarantees parseable output without regex tolerance code; the inverted index turns the per-image tags into an actual search artefact a downstream UI can hit.
