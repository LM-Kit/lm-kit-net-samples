# Microsoft.Extensions.AI Integration Demo

This demo shows how to use LM-Kit.NET through the standard [Microsoft.Extensions.AI](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) abstraction layer (`IChatClient` and `IEmbeddingGenerator`).

## Features

- **Direct chat completion** using `IChatClient.GetResponseAsync` with usage reporting (token counts, finish reason)
- **Streaming chat completion** using `IChatClient.GetStreamingResponseAsync`
- **Embedding generation** using `IEmbeddingGenerator<string, Embedding<float>>`
- **Simple RAG pipeline**: embed facts, search by cosine similarity, augment the prompt with retrieved context
- **ChatOptions**: demonstrates configuring Temperature and MaxOutputTokens

## Prerequisites

- .NET 8.0 SDK
- Sufficient VRAM for `gemma3:4b` (~3 GB) and `embeddinggemma-300m` (~300 MB)

## How It Works

1. **Part 1**: Asks the LLM a personal question ("Who is Elodie's favourite detective?") without any context. The model won't know the answer.
2. **Part 2**: Demonstrates streaming the same question token by token.
3. **Part 3**: Embeds five detective-related facts into a simple in-memory vector store, retrieves the most relevant ones via cosine similarity, and streams an augmented answer grounded in the retrieved context.

## Usage

```bash
dotnet run
```

## NuGet Packages

| Package | Purpose |
|---------|---------|
| `LM-Kit.NET` | Core SDK for local LLM inference |
| `LM-Kit.NET.ExtensionsAI` | Microsoft.Extensions.AI integration (IChatClient, IEmbeddingGenerator) |
| `LM-Kit.NET.Backend.Cuda13.Windows` | NVIDIA GPU acceleration (optional, remove for CPU-only) |
