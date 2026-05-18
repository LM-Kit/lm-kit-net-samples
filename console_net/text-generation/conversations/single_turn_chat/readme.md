# Single-Turn Chat

A demo for stateless question-answering using LM-Kit.NET. Each prompt is handled independently without conversation history.

## Features

- Stateless chat interactions (no context between turns)
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Customizable system prompt
- Real-time streaming responses
- Performance metrics (tokens, speed, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter prompts and receive responses
4. Each prompt is treated as a new, independent request

## When to Use Single-Turn vs Multi-Turn

| Single-Turn | Multi-Turn |
|-------------|------------|
| Independent Q&A | Conversational context needed |
| One-off tasks | Follow-up questions |
| Maximum context for each prompt | Building on previous responses |
| Stateless API-style interactions | Chat-style applications |

## Configuration

```csharp
SingleTurnConversation chat = new(model)
{
    MaximumCompletionTokens = 2048,
    SamplingMode = new RandomSampling()
    {
        Temperature = 0.8f
    },
    SystemPrompt = "You are a helpful assistant."
};
```

## Use Cases

- FAQ bots
- Content generation
- Code completion
- Translation requests
- Single-question answering