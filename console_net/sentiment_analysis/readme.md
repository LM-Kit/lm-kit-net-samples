# Sentiment Analysis

A demo for analyzing text sentiment using LM-Kit.NET with a specialized sentiment analysis model. Classify text as positive or negative with confidence scoring.

## Features

- Fast sentiment classification (positive/negative)
- Specialized lightweight model (~1B parameters)
- Confidence scoring for predictions
- Optional neutral sentiment support
- Minimal VRAM requirements

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Minimal VRAM (~1 GB)

## Usage

1. Run the application
2. The sentiment model loads automatically
3. Enter text to analyze
4. View the sentiment category and confidence score

## Configuration

Enable neutral sentiment detection:

```csharp
SentimentAnalysis classifier = new(model)
{
    NeutralSupport = true  // Enable positive/negative/neutral
};
```

## Example Output

```
Content: I love this product, it works great!
Category: Positive - Elapsed: 0.12 seconds - Confidence: 94.2%

Content: The service was terrible and slow.
Category: Negative - Elapsed: 0.11 seconds - Confidence: 91.8%
```