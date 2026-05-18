# Retrieval Quality Tuning

An interactive demo that indexes a knowledge base once, then lets you run the **same query** under different retrieval configurations and compare results side-by-side. It teaches you how to tune retrieval quality, the most impactful lever in any RAG pipeline.

The knowledge base contains **entirely fictional documentation** for a made-up database called "NebulaDB". This ensures every correct retrieval **must** come from the index, not model memory. The content is designed so that some queries are keyword-heavy (error codes, config parameters), some are semantic (conceptual questions), and some overlap across sections, making each tuning knob visibly impactful.

## Features

- **Three retrieval strategies**: Vector (cosine similarity), BM25 (keyword-based lexical ranking), Hybrid (BM25 + Vector with Reciprocal Rank Fusion)
- **Reranking**: re-score initial results with a second embedding pass, with configurable alpha blending between original and reranked scores
- **MMR diversity filtering**: reduce near-duplicate passages using Maximal Marginal Relevance
- **Context window expansion**: include neighboring partitions around each match for broader context
- **Side-by-side comparison**: the `/compare` command runs the same query across all strategies in one shot
- **Real-time tuning**: change any parameter with a command and immediately see the effect on results

## Prerequisites

- .NET 8.0 or later
- Minimum 2 GB VRAM (embedding model only, no chat model required)
- The embedding model is downloaded automatically on first run

## How It Works

1. **Embedding model loading**: the `embeddinggemma-300m` model is loaded for vector operations
2. **Indexing**: 11 fictional NebulaDB documentation topics are split into chunks (max 300 tokens) and embedded into a `RagEngine`
3. **Interactive loop**: you enter queries and commands to explore how different retrieval configurations affect result quality
4. **Strategy switching**: each command instantly reconfigures the `RagEngine` properties (`RetrievalStrategy`, `MmrLambda`, `ContextWindow`, `Reranker`)

## Usage

1. Run the demo
2. Wait for the embedding model to load and the knowledge base to be indexed
3. Enter a query to see results with the current strategy (default: vector search)
4. Use commands to switch strategies and tune parameters
5. Use `/compare <query>` to see all strategies side-by-side

## Example Session

```
  Query: NDB-4012 timeout error

  === VECTOR ===
  #1 [score=0.724] Error Reference (partition 2)
     ... NDB-4012: Request timeout. The operation exceeded the server-side timeout...
  #2 [score=0.689] Connection Management (partition 1)
     ... connection timeout (default 30 seconds)...

  Query: /bm25
  Strategy: BM25 (keyword-based lexical ranking)

  Query: NDB-4012 timeout error

  === BM25 ===
  #1 [score=0.891] Error Reference (partition 2)
     ... NDB-4012: Request timeout. The operation exceeded the server-side timeout...
  #2 [score=0.634] Replication Troubleshooting (partition 0)
     ... NDB-3001 (Leader Unavailable): occurs during leader election...

  Query: /compare how do I handle connection failures gracefully

  === Comparison ===
  === Vector === (12ms, 5 results)
    #1 [0.812] Connection Management (p1)
    #2 [0.764] Replication and High Availability (p2)
    ...
  === BM25 === (3ms, 5 results)
    #1 [0.543] Connection Management (p1)
    #2 [0.421] Replication Troubleshooting (p0)
    ...
  === Hybrid === (14ms, 5 results)
    #1 [0.038] Connection Management (p1)
    #2 [0.034] Replication and High Availability (p2)
    ...
```

## Commands

### Retrieval Strategies

| Command | Description |
|---------|-------------|
| `/vector` | Switch to vector search (cosine similarity) |
| `/bm25` | Switch to BM25 (keyword-based lexical search) |
| `/hybrid [vw kw]` | Switch to hybrid (vector + BM25 with RRF fusion) |

### Quality Tuning

| Command | Description |
|---------|-------------|
| `/rerank on\|off\|<alpha>` | Toggle reranking or set alpha (0=reranker only, 1=original only) |
| `/mmr <lambda>` | Set MMR diversity (0.0=max diversity, 1.0=pure relevance) |
| `/context <n>` | Include N neighboring partitions around each match |

### Parameters

| Command | Description |
|---------|-------------|
| `/topk <n>` | Max results to retrieve |
| `/minscore <s>` | Minimum relevance score threshold |

### Analysis

| Command | Description |
|---------|-------------|
| `/compare <query>` | Run query across all strategies for side-by-side comparison |
| `/stats` | Show current configuration and index statistics |
| `/help` | Show available commands |

## What to Try

These queries are designed to highlight the strengths and weaknesses of each strategy:

| Query | Best strategy | Why |
|-------|---------------|-----|
| `NDB-4012 timeout error` | BM25 | Exact keyword match on error code |
| `how do I handle connection failures gracefully` | Vector | Semantic meaning, no exact keyword overlap |
| `configure replication for high availability` | Hybrid | Mix of keywords and concepts |
| `backup` | BM25 | Single keyword, benefits from lexical matching |
| `what happens when a leader node goes down` | Vector | Conceptual question about failover |

After trying these, enable reranking (`/rerank on`) and observe how scores change. Then try `/mmr 0.7` to see duplicate sections get deprioritized.

## Key APIs Demonstrated

- `VectorRetrievalStrategy`: cosine similarity search
- `Bm25RetrievalStrategy`: BM25+ keyword-based scoring
- `HybridRetrievalStrategy`: Reciprocal Rank Fusion of vector and BM25 results
- `RagEngine.Reranker` / `RagEngine.RagReranker`: embedding-based re-scoring with alpha blending
- `RagEngine.MmrLambda`: Maximal Marginal Relevance for result diversity
- `RagEngine.ContextWindow`: neighboring partition inclusion
- `RagEngine.FindMatchingPartitions()`: core retrieval method
- `PartitionSimilarity`: result object with `Similarity`, `RawSimilarity`, `RerankedScore`

## Customization

- **Knowledge base**: replace `GetSampleKnowledge()` with your own documents or use `ImportTextFromFile()`
- **Chunk size**: adjust `MaxChunkSize` in the `TextChunking` constructor (smaller = more precise, larger = more context)
- **BM25 parameters**: create `Bm25RetrievalStrategy` with custom `K1`, `B`, `Delta`, and `ProximityWeight`
- **Hybrid weights**: pass custom `VectorWeight` and `KeywordWeight` to `HybridRetrievalStrategy`
- **Rerank alpha**: 0.0 trusts only the reranker, 1.0 trusts only the original score, 0.5 blends equally
