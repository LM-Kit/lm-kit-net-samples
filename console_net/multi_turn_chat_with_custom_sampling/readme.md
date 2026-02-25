# Multi-Turn Chat with Custom Sampling

A demo for fine-tuning text generation behavior using advanced sampling parameters with LM-Kit.NET. Control randomness, repetition, and topic bias.

## Features

- Advanced sampling configuration (temperature, top-k, top-p, min-p)
- Custom sampler sequencing
- Repetition penalty settings
- Logit bias to encourage or discourage specific words
- Dynamic temperature adjustment
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Chat and observe how sampling settings affect responses

## Sampling Parameters

| Parameter | Description |
|-----------|-------------|
| Temperature | Controls randomness (higher = more creative) |
| TopK | Limits to top K most likely tokens |
| TopP | Nucleus sampling threshold |
| MinP | Minimum probability threshold |
| LocallyTypical | Filters tokens by typicality |
| DynamicTemperatureRange | Adjusts temperature based on confidence |

## Repetition Penalty

Prevent repetitive outputs:

```csharp
chat.RepetitionPenalty.RepeatPenalty = 1.5f;
chat.RepetitionPenalty.FrequencyPenalty = 1;
chat.RepetitionPenalty.PresencePenalty = 1;
chat.RepetitionPenalty.TokenCount = 128;
```

## Logit Bias

Encourage or discourage specific topics:

```csharp
// Encourage cat-related words
chat.LogitBias.AddTextChunkBias("cat", 5);
chat.LogitBias.AddTextChunkBias("kitten", 5);

// Discourage dog-related words
chat.LogitBias.AddTextChunkBias("dog", -5);
```

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and start fresh |
| `/regenerate` | Generate a new response to your last input |