# Document RAG

Index every PDF / TXT / DOCX / HTML / EML in a folder into a `RagEngine`, then open a `RagChat` and ask grounded questions with citations.

## What it shows

- `RagEngine.ImportTextFromFile(path, dataSourceId, sectionId)` for batch indexing.
- `RagChat(engine, chatModel)` for the conversational loop.
- `RagChat.QueryGenerationMode = QueryGenerationMode.Contextual` for follow-up rephrasing.
- `RagQueryResult.RetrievedPartitions` for citation rendering.

## Run

```bash
cd console_net/document-intelligence/document-rag/document_rag
dotnet run -- C:\docs\policies
```

## Where this fits

Document RAG is the single most-asked feature in enterprise local-AI deployments. Indexing once and chatting many times keeps PII on premises, gives auditable citations, and runs offline.
