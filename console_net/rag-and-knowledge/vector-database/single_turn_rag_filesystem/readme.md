# Single-Turn RAG with the LM-Kit.NET Built-in Vector Store

A demo for building a single-turn Retrieval-Augmented Generation (RAG) Q&A system using LM-Kit.NET's built-in, file-system-based vector store (`FileSystemVectorStore`). No external database or service is required: collections are persisted to disk and reused across runs. Load documents from URLs, index them, and ask questions answered from the content.

## Features

- RAG pipeline with the built-in LM-Kit.NET vector store (no external dependencies)
- Zero setup: no Docker, server, or connection string
- Load and index documents directly from URLs
- Semantic search to find relevant passages
- Persistent on-disk vector storage for reuse across sessions
- GPU-accelerated retrieval when available

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the chat model (~6 GB for Gemma 4 E4B)

## How It Works

1. Create a `FileSystemVectorStore` pointing at a local directory (created automatically)
2. Load a chat model and an embedding model
3. Import documents from URLs into the store with automatic chunking; each collection is saved as a `.ds` file
4. When a query is submitted, retrieve the most relevant passages
5. Generate a coherent answer grounded in the retrieved content

## Usage

1. Run the application (no external service to start)
2. The demo loads sample eBooks from Project Gutenberg into a local `vector_store` folder
3. Ask questions about the content
4. Receive answers sourced from the indexed documents

On the next run, the previously indexed collections are loaded straight from disk instead of being re-imported.

## When To Use This vs. an External Vector Database

The built-in store is ideal for getting started, local development, single-machine apps, and small-to-medium corpora. For large-scale, distributed, or multi-process deployments, use an external vector database connector instead (see the Qdrant and pgvector demos in this folder).
