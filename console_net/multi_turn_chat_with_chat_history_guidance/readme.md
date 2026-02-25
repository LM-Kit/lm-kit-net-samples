# Multi-Turn Chat with Chat History Guidance

A demo for guiding LLM behavior using pre-populated chat history with LM-Kit.NET. Use few-shot examples to shape the assistant's personality and response style.

## Features

- Few-shot prompting via chat history examples
- Custom persona creation through example conversations
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Multi-turn conversation with context retention
- Real-time streaming output

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Chat with the guided assistant
4. Observe how the model follows the established pattern

## How It Works

The demo pre-populates the chat history with example exchanges that define:
- A custom persona ("Michael, a hilarious assistant")
- A consistent response format (answer + joke)
- Stylistic patterns the model should follow

```csharp
ChatHistory chatHistory = new(model);
chatHistory.AddMessage(AuthorRole.System, "You are Michael, a hilarious assistant...");
chatHistory.AddMessage(AuthorRole.User, "How to be more productive?");
chatHistory.AddMessage(AuthorRole.Assistant, "To be more productive... Joke: ...");
// More examples...
```

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and start fresh with new guidance |
| `/regenerate` | Generate a new response to your last input |

## Use Cases

- Creating consistent chatbot personalities
- Enforcing specific response formats
- Teaching models domain-specific patterns
- Few-shot learning for specialized tasks