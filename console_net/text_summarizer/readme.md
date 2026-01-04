# Text Summarizer

A demo for summarizing text files using LM-Kit.NET language models. Generate titles and concise summaries from long-form text content.

## Features

- Summarize text files with automatic title generation
- Configurable maximum summary length
- Confidence scoring for summary quality
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS
- Word count statistics (input vs. output)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (0.8–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter the path to a text file
4. View the generated title and summary

## Configuration

Customize the summarizer output:

```csharp
Summarizer summarizer = new(model)
{
    GenerateContent = true,
    GenerateTitle = true,
    MaxContentWords = 100
};
```

## Example Output

```
Summarizing content with 2500 words to 100 max words...

Title: Climate Change Impact Assessment 2024
Summary: The report highlights accelerating global temperature increases 
and their effects on ecosystems worldwide. Key findings include rising 
sea levels, increased extreme weather events, and biodiversity loss. 
Recommendations focus on renewable energy adoption and carbon reduction.

Summarization completed in 3.45 seconds | Summary word count: 42 | Confidence: 0.89
```

## Supported Formats

- Plain text files (.txt)
- Any UTF-8 encoded text content