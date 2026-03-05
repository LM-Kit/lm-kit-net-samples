# Code Analysis Assistant

A multi-turn code analysis assistant that uses built-in file system and web search tools to read, navigate, and understand codebases through natural conversation.

## Features

- **Built-in tool calling**: the model reads files, lists directories, searches code, and searches the web autonomously
- Multi-turn conversation with full history
- Real-time streaming with color-coded output (reasoning, tool calls, normal text)
- Performance metrics after each response (tokens, speed, context usage)
- Support for multiple coding-focused LLMs

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (7-18 GB depending on model choice)

## How It Works

The demo creates a `MultiTurnConversation` with four built-in tools registered:

| Tool | Description |
|------|-------------|
| `FileSystemRead` | Read the contents of source files |
| `FileSystemList` | List files and subdirectories |
| `FileSystemSearch` | Search for files by name or pattern |
| `WebSearch` | Search the web via DuckDuckGo (no API key required) |

When you ask a question about code, the model decides which tools to call, reads the relevant files, and answers based on what it finds.

## Usage

1. Run the application
2. Select a language model
3. Ask questions about your code, point it at files or directories
4. Use `/reset` to clear history or `/regenerate` to redo the last response
5. Type `q` to quit

## Example Prompts

```
> Read Program.cs in the current directory and explain what it does
> List the files in ../src and tell me what this project is about
> Search for files named *.csproj and summarize the project structure
> What does the FileSystemRead tool do? Search the web for LM-Kit.NET built-in tools
```

## Supported Models

| # | Model | VRAM |
|---|-------|------|
| 0 | Alibaba Qwen 3.5 9B | ~7 GB |
| 1 | OpenAI GPT OSS 20B | ~16 GB |
| 2 | Mistral Devstral Small 2 24B | ~16 GB |
| 3 | **Alibaba Qwen 3 Coder 30B-A3B** [Recommended] | ~18 GB |
| 4 | Z.ai GLM 4.7 Flash 30B | ~18 GB |

You can also enter any custom model URI or model ID.

## Understanding the Output

- **Blue**: internal reasoning
- **Magenta**: tool invocations
- **White**: normal response text

## Minimal Integration

```csharp
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration;

using LM model = LM.LoadFromModelID("qwen3-coder:30b-a3b");

var chat = new MultiTurnConversation(model)
{
    SystemPrompt = "You are a coding assistant. Use your tools to read files before answering."
};

chat.Tools.Register(BuiltInTools.FileSystemRead);
chat.Tools.Register(BuiltInTools.FileSystemList);
chat.Tools.Register(BuiltInTools.FileSystemSearch);
chat.Tools.Register(BuiltInTools.WebSearch);

var result = chat.Submit("Read Program.cs and explain what it does");
Console.WriteLine(result.Completion);
```
