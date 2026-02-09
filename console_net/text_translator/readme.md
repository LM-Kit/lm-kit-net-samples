# Text Translator

A demo for translating text between languages using LM-Kit.NET. Automatically detect the source language and translate to your target language.

## Features

- Text translation with automatic source language detection
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Real-time streaming translation output
- Multilingual support (depends on model capabilities)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter text in any language
4. View the automatic language detection and translation

## Configuration

Change the target language by modifying the destination:

```csharp
Language destLanguage = Language.French;  // or Spanish, German, etc.
```

## Supported Languages

Language support depends on the selected model. Most multilingual models support major languages including English, Spanish, French, German, Chinese, Japanese, and many more.

## Example

```
Enter a text to translate in English:

Bonjour, comment allez-vous aujourd'hui?

Detecting language...
Translating from French...
Hello, how are you today?
```