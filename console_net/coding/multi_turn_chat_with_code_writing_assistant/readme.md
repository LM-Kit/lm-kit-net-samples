# Code Writing Assistant

A multi-turn assistant that can read, create, and modify source files through natural conversation. Unlike the read-only Coding Assistant, this demo gives the model write access to your file system so it can generate new code, refactor existing files, and scaffold projects.

## Features

- **File read and write**: the model reads existing files, creates new ones, and writes modified versions back to disk
- **Directory listing and file search**: navigate and explore codebases autonomously
- **Web search**: look up documentation, APIs, and solutions online
- Multi-turn conversation with full history
- Real-time streaming with color-coded output (reasoning, tool calls, normal text)
- Performance metrics after each response (tokens, speed, context usage)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (7-18 GB depending on model choice)

## How It Works

The demo creates a `MultiTurnConversation` with five built-in tools registered:

| Tool | Description |
|------|-------------|
| `FileSystemRead` | Read the contents of source files |
| `FileSystemList` | List files and subdirectories |
| `FileSystemSearch` | Search for files by name or pattern |
| `FileSystemWrite` | Create new files or overwrite existing ones |
| `WebSearch` | Search the web via DuckDuckGo (no API key required) |

When you ask the model to write or modify code, it reads the relevant files first, generates the updated content, and writes it back using `FileSystemWrite`. The system prompt instructs the model to confirm file paths before creating new files and to explain its changes.

## Usage

1. Run the application
2. Select a language model
3. Ask it to create, modify, or refactor code
4. Use `/reset` to clear history or `/regenerate` to redo the last response
5. Type `q` to quit

## Example Prompts

```
> Create a new C# class called UserService in ./Services/UserService.cs with CRUD methods
> Read Program.cs and add proper error handling to the Main method
> Scaffold a basic ASP.NET minimal API project in ./my-api/ with a health check endpoint
> Search for all *.cs files and add XML doc comments to any public methods that are missing them
> Look up the latest System.Text.Json serialization best practices and update my JsonHelper.cs
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
    SystemPrompt = "You are a code writing assistant. Read files before modifying them."
};

chat.Tools.Register(BuiltInTools.FileSystemRead);
chat.Tools.Register(BuiltInTools.FileSystemList);
chat.Tools.Register(BuiltInTools.FileSystemSearch);
chat.Tools.Register(BuiltInTools.FileSystemWrite);
chat.Tools.Register(BuiltInTools.WebSearch);

var result = chat.Submit("Create a new file hello.cs with a Hello World program");
Console.WriteLine(result.Completion);
```
