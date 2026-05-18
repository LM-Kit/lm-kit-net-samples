# Web Content Info Extractor to JSON

A demo for extracting and summarizing web page content into structured JSON using LM-Kit.NET. Analyze any URL and get formatted metadata about the page.

## Features

- Fetch and analyze web pages by URL
- Extract structured information with JSON grammar constraints
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Guaranteed valid JSON output
- Real-time streaming response

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (0.8–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter a web page URL
4. View the extracted JSON metadata

## Extracted Fields

| Field | Description |
|-------|-------------|
| Primary Topic | Main subject or theme of the content |
| Domain or Field | Area of knowledge or industry |
| Language | Language the content is written in |
| Audience | Intended target audience |

## Example Output

```json
{
  "Primary Topic": "Machine Learning Frameworks",
  "Domain or Field": "Technology / Software Development",
  "Language": "English",
  "Audience": "Software developers and data scientists"
}
```

## JSON Grammar

The demo uses LM-Kit's grammar feature to ensure valid JSON output:

```csharp
chat.Grammar = Grammar.CreateJsonGrammarFromTextFields(
    new string[] { "Primary Topic", "Domain or Field", "Language", "Audience" }
);
```