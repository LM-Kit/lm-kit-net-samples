# Multi-Turn Chat with Persistent Session

A demo for saving and restoring chat sessions across application restarts using LM-Kit.NET. Continue conversations exactly where you left off.

## Features

- Save chat sessions to disk
- Restore previous conversations on startup
- Full chat history preservation
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS
- Real-time streaming responses

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Chat with the assistant
4. Press Enter on an empty prompt to save and exit
5. Restart the application to continue the previous conversation

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and start fresh |
| `/regenerate` | Generate a new response to your last input |
| (empty) | Save session and exit |

## How It Works

Sessions are automatically saved when you exit:

```csharp
// Save session on exit
chat.SaveSession("session.bin");

// Restore session on startup
if (File.Exists(sessionPath))
{
    chat = new MultiTurnConversation(model, sessionPath);
}
```

## Session Files

Session files are named based on the model: `session{ModelName}.bin`

This allows maintaining separate conversation histories for different models.

## Use Cases

- Long-running research conversations
- Ongoing project discussions
- Personal assistant with memory
- Customer support with context preservation