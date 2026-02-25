# Single-Turn RAG with Qdrant

A demo for building a single-turn Retrieval-Augmented Generation (RAG) Q&A system using LM-Kit.NET with Qdrant as the vector database. Load documents from URLs, index them, and ask questions answered from the content.

## Features

- RAG pipeline with Qdrant vector store integration
- Load and index documents directly from URLs
- Semantic search to find relevant passages
- Persistent vector storage for reuse across sessions
- GPU-accelerated retrieval when available

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Qdrant instance running locally or remotely
- Sufficient VRAM for the chat model (~4 GB for Gemma 3 4B)

## Qdrant Setup

Start a local Qdrant instance using Docker:

```bash
docker run -p 6333:6333 -p 6334:6334 qdrant/qdrant
```

See the [Qdrant quickstart guide](https://qdrant.tech/documentation/quickstart/) for more options.

## How It Works

1. Connect to a Qdrant vector store
2. Load a chat model and an embedding model
3. Import documents from URLs into Qdrant with automatic chunking
4. When a query is submitted, retrieve the most relevant passages
5. Generate a coherent answer grounded in the retrieved content

## Usage

1. Start your Qdrant instance
2. Run the application
3. The demo loads sample eBooks from Project Gutenberg
4. Ask questions about the content
5. Receive answers sourced from the indexed documents