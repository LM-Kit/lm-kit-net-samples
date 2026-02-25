# Help Desk Knowledge Base

A realistic customer support knowledge base that demonstrates the full lifecycle of a production RAG system: persistent storage, incremental article management, category-scoped retrieval, and grounded answer generation with source attribution.

The knowledge base contains **fictional documentation** for a made-up SaaS product called "CloudPeak". It is seeded with 20 support articles across 5 categories on first run, persisted to disk, and reloaded instantly on subsequent runs without re-embedding.

## Features

- **Persistent file-backed storage**: the knowledge base is saved to a `.dat` file and reloaded on restart without re-embedding
- **Incremental article management**: add new articles, remove outdated ones, and list indexed content at runtime
- **Category-scoped retrieval**: use `DataFilter` to restrict queries to a specific category (e.g., search only "Billing" articles)
- **Markdown-aware chunking**: articles are chunked using `MarkdownChunking` which respects heading boundaries
- **Answer generation with source attribution**: retrieved passages are fed to a chat model via `QueryPartitions`, with source articles displayed
- **Two-step retrieval + generation pipeline**: explicitly shows the `FindMatchingPartitions` then `QueryPartitions` flow

## Prerequisites

- .NET 8.0 or later
- Minimum 6 GB VRAM (Qwen-3 8B) or 4 GB VRAM (Gemma 3 4B)
- Models are downloaded automatically on first run

## How It Works

1. **Model loading**: a chat model (user-selected) and an embedding model (`embeddinggemma-300m`) are loaded
2. **Knowledge base initialization**: if no `.dat` file exists, 20 support articles are indexed with `MarkdownChunking` and persisted. On subsequent runs, the file is loaded directly (no re-embedding).
3. **Query pipeline**: on each question:
   - A `DataFilter` is applied if a category scope is set (returns `true` to exclude non-matching sections)
   - `FindMatchingPartitions` retrieves the top-5 most relevant passages
   - Source articles are displayed with scores
   - `QueryPartitions` feeds the passages into the chat model to generate a grounded answer
4. **Article management**: articles can be added or removed at any time; changes are automatically persisted to the `.dat` file

## Usage

1. Run the demo
2. Select a chat model from the menu
3. Wait for models to load and the knowledge base to initialize
4. Ask support questions about the fictional CloudPeak product
5. Use commands to manage articles and scope queries

## Example Session

```
  You: How do I reset my password?

  Retrieved 3 passages in 18ms:
    Account Management > Password Reset (score: 0.91, 2 chunk(s))
    Account Management > Account Recovery (score: 0.72, 1 chunk(s))

  Assistant: To reset your CloudPeak password, go to cloudpeak.io/reset,
  enter your account email, and click "Send Reset Link". You'll receive
  a reset email within a few minutes. The link expires after 30 minutes.
  Your new password must be at least 12 characters with mixed case,
  numbers, and a special character.

  You: /scope Billing
  Scope set to "Billing". Queries will only search articles in this category.

  [Billing] You: What is the refund policy?
  Searching in: Billing
  Retrieved 2 passages in 12ms:
    Billing > Refund Policy (score: 0.93, 2 chunk(s))

  Assistant: CloudPeak offers refunds within 14 days of your initial
  subscription or upgrade, no questions asked. Refunds for service
  outages exceeding 4 hours are also available. Email billing@cloudpeak.io
  with your account email and invoice number...

  You: /add Billing Cancellation Policy
  Enter article content (type END on a new line to finish):
  # Cancellation Policy
  You can cancel your subscription at any time...
  END
  Added "Cancellation Policy" to Billing (2 chunks, 0.8s)

  You: /remove "Cancellation Policy"
  Removed "Cancellation Policy" from Billing.
```

## Commands

### Querying

| Command | Description |
|---------|-------------|
| `<question>` | Search the knowledge base and generate an answer |
| `/scope <category>` | Restrict queries to a specific category |
| `/scope all` | Remove scope restriction |
| `/sources` | Toggle source attribution display |

### Article Management

| Command | Description |
|---------|-------------|
| `/add <category> <title>` | Add a new article (enter content interactively) |
| `/addfile <category> <title> <path>` | Add an article from a Markdown file |
| `/remove <title>` | Remove an article by title |

### Browsing

| Command | Description |
|---------|-------------|
| `/categories` | Show category summary with article counts |
| `/list [category]` | List all articles, optionally filtered by category |
| `/stats` | Show knowledge base size, storage, and configuration |
| `/help` | Show available commands |

## Key APIs Demonstrated

- `DataSource.CreateFileDataSource()`: create a persistent, file-backed knowledge base
- `DataSource.LoadFromFile()`: reload a persisted knowledge base without re-embedding
- `DataSource.HasSection()` / `DataSource.RemoveSection()`: incremental article management
- `DataFilter(sectionFilter)`: category-scoped retrieval via section filtering
- `MarkdownChunking`: Markdown-aware chunking that respects heading boundaries
- `RagEngine.FindMatchingPartitions()`: retrieve relevant passages
- `RagEngine.QueryPartitions()`: generate a grounded answer from retrieved passages
- `SingleTurnConversation`: stateless chat for answer generation

## Knowledge Base Categories

| Category | Articles | Topics |
|----------|----------|--------|
| Account Management | 5 | Password reset, recovery, profile, 2FA, deletion |
| Billing | 4 | Plans, payments, refunds, invoices |
| Technical Support | 5 | Connectivity, errors, export/import, API limits, performance |
| Getting Started | 3 | Quick start, system requirements, first project |
| Security | 3 | Data privacy, encryption, compliance |

## Customization

- **Replace the seed articles**: modify `GetSeedArticles()` with your own support documentation
- **Change the chat model**: select a different model at startup or pass a custom URI
- **Adjust chunking**: change `MaxChunkSize` in the `MarkdownChunking` constructor
- **Add hybrid search**: set `ragEngine.RetrievalStrategy = new HybridRetrievalStrategy()` for combined vector + keyword search
- **Enable reranking**: set `ragEngine.Reranker = new RagEngine.RagReranker(embeddingModel, 0.5f)`
