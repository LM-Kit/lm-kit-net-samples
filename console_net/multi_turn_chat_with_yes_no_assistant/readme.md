# Multi-Turn Chat with Yes/No Assistant

A demo for creating a constrained-output chatbot that only responds with "yes" or "no" using LM-Kit.NET grammar constraints.

## Features

- Boolean-only responses enforced by grammar rules
- Fact-checking chatbot use case
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS
- Greedy decoding for deterministic outputs
- Multi-turn conversation with context

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Ask yes/no questions
4. The assistant responds only with "yes" or "no"

## How It Works

The demo uses a predefined Boolean grammar to constrain outputs:

```csharp
MultiTurnConversation chat = new(model)
{
    MaximumCompletionTokens = 20,
    SamplingMode = new GreedyDecoding(),
    SystemPrompt = "You are a fact-checking chatbot. Respond only with yes or no.",
    Grammar = new Grammar(Grammar.PredefinedGrammar.Boolean)
};
```

## Example Conversation

```
User: Is the Earth round?
Assistant: yes

User: Is the sky green?
Assistant: no

User: Can humans breathe underwater without equipment?
Assistant: no
```

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and start fresh |

## Use Cases

- Fact verification bots
- Decision support systems
- Quick validation queries
- Binary classification interfaces
- Survey and polling applications