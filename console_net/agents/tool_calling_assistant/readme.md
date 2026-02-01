# Multi-Turn Chat with Tools

A demo for multi-turn conversations with custom tool calling using LM-Kit.NET. Extend your local LLM with custom functions that the model can invoke during conversations.

## Features

- Multi-turn chat with function calling / tool use
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS
- Sample tools included:
  - **Currency Tool**: Currency conversion
  - **Weather Tool**: Weather information lookup
  - **Unit Conversion Tool**: Convert between measurement units
- Easy-to-extend tool registration system
- Real-time display of tool invocations and reasoning

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Chat with the assistant and ask it to use the available tools
4. Watch the model reason and invoke tools as needed

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear chat history and start fresh |
| `/regenerate` | Generate a new response to your last input |
| `/continue` | Continue the last assistant response |

## Adding Custom Tools

Create a new tool class and register it with the chat:

```csharp
chat.Tools.Register(new MyCustomTool());
```

See the `Tools/` folder for implementation examples.