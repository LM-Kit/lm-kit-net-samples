# Sarcasm Detection

A demo for detecting sarcasm in text using LM-Kit.NET with a specialized fine-tuned model. Classify text as sarcastic or sincere.

## Features

- Fast sarcasm detection (sarcastic/sincere)
- Specialized fine-tuned model for English
- Confidence scoring for predictions
- Lightweight model with minimal VRAM requirements

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Minimal VRAM (~1 GB)

## Usage

1. Run the application
2. The sarcasm detection model loads automatically
3. Enter text to analyze
4. View whether the text is sarcastic and the confidence score

## Example Output

```
Content: Oh great, another meeting that could have been an email.
Is sarcastic: True - Elapsed: 0.15 seconds - Confidence: 92.3%

Content: Thank you so much for your help, I really appreciate it.
Is sarcastic: False - Elapsed: 0.12 seconds - Confidence: 88.7%

Content: Wow, standing in line for an hour was exactly how I wanted to spend my day.
Is sarcastic: True - Elapsed: 0.14 seconds - Confidence: 94.1%
```

## Language Support

The included model has been fine-tuned specifically for English. For other languages, additional model fine-tuning may be required.

## Use Cases

- Social media sentiment analysis
- Customer feedback classification
- Content moderation
- Tone detection in communications