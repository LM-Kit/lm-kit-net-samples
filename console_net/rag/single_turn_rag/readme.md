# Single-Turn RAG

A demo for building a single-turn Retrieval-Augmented Generation (RAG) Q&A system using LM-Kit.NET. Load text documents and eBooks, then ask questions answered from the indexed content.

## Features

- Index text documents and eBooks into a local vector database
- Semantic search to find relevant passages
- RAG pipeline combining retrieval with LLM generation
- Persistent data source for reuse across sessions
- GPU-accelerated retrieval when available

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the chat model (~4 GB for Gemma 3 4B)

## How It Works

1. Load a chat model and an embedding model
2. Import text documents into a vector database with automatic chunking
3. When a query is submitted, retrieve the most relevant passages
4. Generate a coherent answer grounded in the retrieved content

## Usage

1. Run the application
2. The demo loads sample eBooks (Romeo and Juliet, Moby Dick, Pride and Prejudice)
3. Ask questions about the content
4. Receive answers sourced from the indexed documents

## Adding Your Own Documents

Modify the code to load your own text files:

```csharp
LoadEbook("your_document.txt", "Document Title");
```

Documents are chunked and embedded automatically for semantic retrieval.