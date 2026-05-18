# Text Translator

A demo for translating text and images between languages using LM-Kit.NET. Supports both text input and image-based translation with automatic source language detection.

## Features

- **Text translation** with automatic source language detection
- **Image translation**: extract and translate text from images (PNG, JPG, BMP, TIFF, WebP)
- Support for translation-specialized models (TranslateGemma) and general-purpose LLMs
- Real-time streaming translation output
- Interactive target language selection with 12 preset languages
- Change target language mid-session with `/lang`

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3-18 GB depending on model choice)
- A vision-capable model is required for image translation (e.g., TranslateGemma)

## How It Works

1. **Model selection**: choose a translation-specialized model (TranslateGemma 3) or a general-purpose LLM
2. **Language selection**: pick the target language from a preset list
3. **Input**: enter text directly or provide an image file path
4. **Detection**: the source language is automatically detected
5. **Translation**: text is translated (or extracted from image and translated) using the loaded model

## Usage

1. Run the application
2. Select a model (TranslateGemma models are suggested for best translation quality)
3. Choose a target language
4. Enter text or an image path to translate
5. Use `/lang` to switch target language, `/quit` to exit

## Example: Text Translation

```
[English] > Bonjour, comment allez-vous aujourd'hui?

Detecting language...
Detected: French. Translating to English...

Hello, how are you today?
```

## Example: Image Translation

```
[Spanish] > C:\photos\french_sign.png

Loading image...
Image loaded (1920x1080).
Detecting language...
Detected: French. Translating to Spanish...

Zona peatonal - Prohibido el paso de vehiculos
```

## Supported Models

### Translation-Specialized (Suggested)

| Model | VRAM | Description |
|-------|------|-------------|
| TranslateGemma 3 4B | ~3 GB | Lightweight, 55-language translation with vision |
| TranslateGemma 3 12B | ~8 GB | High-quality, 55-language translation with vision |

### General-Purpose

| Model | VRAM |
|-------|------|
| Alibaba Qwen 3.5 9B | ~7 GB |
| Google Gemma 4 E4B | ~6 GB |
| Alibaba Qwen 3.6 27B | ~18 GB |

## Supported Languages

12 preset target languages: English, French, Spanish, German, Italian, Portuguese, Chinese (Simplified), Japanese, Korean, Arabic, Russian, Hindi. Source language is detected automatically from a much wider set.
