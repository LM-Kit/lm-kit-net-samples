# Emotion Detection

A demo for detecting emotions in text using LM-Kit.NET. Classify text into emotion categories with confidence scoring.

## Features

- Emotion classification: happiness, anger, sadness, fear, neutral
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Confidence scoring for predictions
- Optional neutral emotion support
- Performance metrics

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter text to analyze
4. View the detected emotion and confidence score

## Emotion Categories

- **Happiness**: Joy, excitement, contentment
- **Anger**: Frustration, irritation, rage
- **Sadness**: Grief, disappointment, melancholy
- **Fear**: Anxiety, worry, dread
- **Neutral**: No strong emotion detected

## Configuration

Disable neutral detection for strict emotion classification:

```csharp
EmotionDetection classifier = new(model)
{
    NeutralSupport = false  // Only happiness/anger/sadness/fear
};
```