# Custom Classification

A demo for classifying text into custom categories using LM-Kit.NET. Define your own classification labels and categorize any text content.

## Features

- Text classification with customizable categories
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Confidence scoring for classification results
- Fast inference with performance metrics

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter text to classify
4. View the predicted category and confidence score

## Default Categories

The demo includes sample categories:
- Food and recipes
- Technology
- Health
- Sport
- Politics
- Business
- Environment
- Movies and TV shows
- Books and literature

## Customizing Categories

Modify the `CLASSIFICATION_CATEGORIES` array to define your own labels:

```csharp
static readonly string[] CLASSIFICATION_CATEGORIES = {
    "positive",
    "negative",
    "neutral"
};
```