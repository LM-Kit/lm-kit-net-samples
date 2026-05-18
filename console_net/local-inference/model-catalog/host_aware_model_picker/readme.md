# Model Catalog Browser

Programmatic discovery of every model shipped by the LM-Kit catalog. Filter by capability, see whether each model fits on the host's GPU, sort by size, load by `ModelID`.

## What it shows

- `ModelCard.GetPredefinedModelCards()` enumerates the catalog at runtime.
- `ModelCard.Capabilities` is a `[Flags]` enum: `Chat`, `Vision`, `OCR`, `TextEmbeddings`, `ImageEmbeddings`, `SpeechToText`, `Reasoning`, `ToolsCall`, `TextReranking`, `Translation`, etc.
- `DeviceConfiguration.GetPerformanceScore(ModelCard)` returns a 0..1 score for how well the model fits the current host. The demo maps it to a coloured `good / ok / tight / no` indicator.
- `Runtime.Backend`, `Runtime.HasGpuSupport`, `GpuDeviceInfo.Devices` are printed at startup so you see the host context before scoring.
- `LM.LoadFromModelID(modelID, ...)` loads the selected card and downloads on first use.
- `DeviceConfiguration.GetOptimalContextSize(LM)` reports the largest context the host can comfortably hold for the loaded model.

## Run

```bash
cd console_net/local-inference/model-catalog/model_catalog
dotnet run
```

Pick a capability, read the table (rows in green fit comfortably, red rows do not), then pick a row to load.

## Where this fits

Hard-coded model IDs do not scale. With the catalog API plus the fit score, an app can ship a "smallest model that supports my capability AND fits on this host" selector that adapts automatically as users upgrade hardware or a new model is added to the catalog.
