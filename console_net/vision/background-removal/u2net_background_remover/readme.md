# U2Net Background Remover

Interactive console app that produces transparent-background PNGs from input images. Single image or whole folder, fully on-device via the bundled U2-Net segmentation model.

## What it shows

- `LM.LoadFromModelID("u2net")` for the segmentation model.
- `LMKit.Segmentation.BackgroundDetection(model)` engine.
- `BackgroundDetection.RemoveBackground(ImageBuffer)` synchronous API (`RemoveBackgroundAsync` also exists).
- `ImageBuffer.LoadAsRGB(path)` to read, `SaveAsPng(path)` to write.
- Two interactive modes from a menu:
  - **File**: cut out a single image.
  - **Folder**: process every supported image in a folder.

## Run

```bash
cd console_net/vision/background-removal/u2net_background_remover
dotnet run
```

No command-line arguments. The model loads once at startup. Pick a mode from the menu and follow the prompts.

## Where this fits

Customer-uploaded photos never leave the device. No vendor API, no per-image cost, predictable latency. Suits commerce listing photos, identity / KYC document capture, retouching pipelines, and augmented marketing workflows.
