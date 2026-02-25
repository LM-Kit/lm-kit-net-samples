# Multi-Turn Chat

A demo for conversational AI with context retention using LM-Kit.NET. Build interactive chatbots that remember previous messages in the conversation.

## Features

- Multi-turn conversation with full context history
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Regenerate or continue responses
- Customizable system prompt
- Real-time streaming output
- Performance metrics (tokens, speed, quality score)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Chat with the assistant
4. Previous messages are retained for context

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and start fresh |
| `/regenerate` | Generate a new response to your last input |
| `/continue` | Continue the last assistant response |

## Configuration

```csharp
MultiTurnConversation chat = new(model)
{
    MaximumCompletionTokens = 2048,
    SamplingMode = new RandomSampling()
    {
        Temperature = 0.8f
    },
    SystemPrompt = "You are a helpful assistant."
};
```

## Key Concepts

- **Context retention**: Each message builds on the conversation history
- **Temperature**: Controls response randomness (0 = deterministic, 1 = creative)
- **Token limits**: Maximum tokens for generated responses
- **System prompt**: Sets the assistant's behavior and personality

## Use Cases

- Customer support chatbots
- Personal assistants
- Interactive tutoring
- Conversational interfaces