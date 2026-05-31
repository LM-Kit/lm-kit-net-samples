# Single-Turn RAG with pgvector

A demo for building a single-turn Retrieval-Augmented Generation (RAG) Q&A system using LM-Kit.NET with PostgreSQL + the pgvector extension as the vector database. Load documents from URLs, index them, and ask questions answered from the content.

## Features

- RAG pipeline with PostgreSQL pgvector vector store integration
- Load and index documents directly from URLs
- Semantic search (cosine similarity) to find relevant passages
- Persistent vector storage for reuse across sessions
- Automatic database, extension, schema, and index provisioning
- GPU-accelerated retrieval when available

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- A PostgreSQL instance with the [pgvector](https://github.com/pgvector/pgvector) extension available
- Sufficient VRAM for the chat model (~6 GB for Gemma 4 E4B)

## pgvector Setup

The easiest way to get a PostgreSQL server with pgvector preinstalled is the official Docker image. Host port 5433 is used here to avoid clashing with a native PostgreSQL on 5432:

```bash
docker run --name lmkit-pgvector -e POSTGRES_PASSWORD=postgres -p 5433:5432 -d pgvector/pgvector:pg17
```

The demo creates the target database on demand via `PgVectorEmbeddingStore.EnsureDatabaseExistsAsync(...)` (the connecting role needs the `CREATEDB` privilege), then the connector creates the `vector` extension, schema, tables, and indexes automatically on first use.

To use an existing PostgreSQL server instead, install pgvector into it (see the pgvector installation guide) and update the connection string in `Program.cs`.

## How It Works

1. Connect to a PostgreSQL + pgvector vector store
2. Load a chat model and an embedding model
3. Import documents from URLs into pgvector with automatic chunking
4. When a query is submitted, retrieve the most relevant passages by cosine similarity
5. Generate a coherent answer grounded in the retrieved content

## Usage

1. Start your PostgreSQL + pgvector instance
2. Adjust the connection string in `Program.cs` if needed
3. Run the application
4. The demo loads sample eBooks from Project Gutenberg
5. Ask questions about the content
6. Receive answers sourced from the indexed documents
