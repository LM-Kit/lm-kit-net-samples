# Conversational RAG

A multi-turn conversational chatbot that answers questions using Retrieval-Augmented Generation (RAG). This demo showcases the `RagChat` class, which combines a user-managed `RagEngine` with an internal multi-turn conversation to deliver grounded, context-aware responses.

The knowledge base contains **entirely fictional data** about a made-up company called "NovaPulse Technologies". Because none of this information exists on the internet or in the model's training data, every correct answer **must** come from retrieval. This makes it easy to verify that RAG is working: if the model answers with specific NovaPulse facts, the retrieval pipeline found the right context.

## Features

- **Fictional knowledge base**: five distinct topics (company overview, products, HR policies, earnings, roadmap) that the model cannot know from training
- **Multi-turn conversation**: follow-up questions are automatically contextualized using conversation history
- **Four query generation modes**: Original, Contextual, Multi-Query (with RRF fusion), and HyDE
- **Real-time streaming**: responses are streamed token by token with color-coded output
- **Retrieval telemetry**: partition counts, similarity scores, and timing displayed for every query
- **Interactive commands**: switch modes, adjust retrieval parameters, and view configuration at runtime

## Prerequisites

- .NET 8.0 or later
- Minimum 6 GB VRAM (Qwen-3 8B) or 4 GB VRAM (Gemma 3 4B with custom selection)
- Models are downloaded automatically on first run

## How It Works

1. **Model loading**: a chat model (user-selected) and an embedding model (`embeddinggemma-300m`) are loaded
2. **Indexing**: five topic articles about the fictional NovaPulse company are split into chunks (max 400 tokens) and embedded into the `RagEngine`
3. **RagChat initialization**: a `RagChat` instance wraps the engine with a multi-turn conversation
4. **Query loop**: on each question, `RagChat.SubmitAsync` orchestrates:
   - Query contextualization (rewrites follow-ups into standalone queries)
   - Retrieval dispatch (selects the best partitions from the knowledge base)
   - Prompt construction (injects retrieved context into a template)
   - Response generation (produces a grounded answer via the internal conversation)

## Usage

1. Run the demo
2. Select a chat model from the menu (press Enter for the recommended default)
3. Wait for models to load and the knowledge base to be indexed
4. Ask questions about NovaPulse: its products, employees, financials, or roadmap
5. Use follow-up questions to test contextual query rewriting

## Example Conversation

```
You: What products does NovaPulse sell?
Assistant: NovaPulse offers three navigation products built on their QPP platform:
the QNav-100 (entry-level, CHF 38,000), the QNav-500 (commercial-grade, CHF 145,000),
and the QNav-900X (defense/deep-space, ~CHF 400,000+)...

You: What accuracy does the cheapest one provide?
Assistant: The QNav-100, which is the entry-level module at CHF 38,000, provides
2.4 mm positioning accuracy...

You: How many vacation days do employees get?
Assistant: Full-time NovaPulse employees receive 27 days of paid vacation per year,
plus 5 additional personal days. Unused days can be carried over for up to one
calendar year...
```

## Commands

| Command  | Description                                  |
|----------|----------------------------------------------|
| `/reset` | Clear conversation history                   |
| `/mode`  | Switch query generation mode                 |
| `/topk`  | Change max retrieved partitions              |
| `/stats` | Show current configuration                   |
| `/help`  | Show available commands                      |

## Customization

- **Query generation mode**: switch between Original, Contextual, Multi-Query, and HyDE via `/mode`
- **Retrieval tuning**: adjust `MaxRetrievedPartitions` and `MinRelevanceScore` in code or via `/topk`
- **Knowledge base**: replace `GetSampleKnowledge()` with your own data or file imports
- **System prompt**: modify `chat.SystemPrompt` for domain-specific behavior
- **Engine settings**: configure `chat.Engine.MmrLambda`, `chat.Engine.ContextWindow`, or `chat.Engine.RetrievalStrategy` for advanced control
