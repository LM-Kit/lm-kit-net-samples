# RAG Query Strategies Lab (Multi-Query / HyDE)

Interactive console app that compares three retrieval strategies (Original, MultiQuery, HypotheticalAnswer / HyDE) on the same RAG index. Bring your own corpus or start with the built-in 7-passage seed.

## What it shows

- `RagChat.QueryGenerationMode`: `Original`, `MultiQuery`, `HypotheticalAnswer`.
- `RagChat.MultiQueryOptions.QueryVariantCount`.
- `RagChat.HydeOptions.MaxCompletionTokens`.
- `RagQueryResult.RetrievedPartitions` to see what each strategy actually pulls.
- Four interactive modes from a menu:
  - **Compare**: run a question across all three strategies side-by-side.
  - **Import**: paste passages to add to the index.
  - **ImportFile**: load passages from a UTF-8 text file.
  - **Reset**: wipe and reseed the default corpus.

## Run

```bash
cd console_net/rag-and-knowledge/query-expansion/query_strategies_lab
dotnet run
```

Both models load once at startup. The default seven-passage corpus is preloaded.

## Where this fits

Naive RAG drops recall the moment a user phrases the question differently from the corpus. MultiQuery and HyDE are the two cheapest, well-known recall fixes. The lab makes the lift visible on your own data.
