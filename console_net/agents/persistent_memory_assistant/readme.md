# Persistent Memory Assistant

A demo showcasing LM-Kit.NET's built-in automatic memory extraction for building AI assistants that remember context across conversations. The agent uses `AgentMemory` with LLM-based extraction to automatically identify and store relevant facts from each conversation turn.

## Features

- **Automatic memory extraction**: the LLM analyzes each conversation turn and stores relevant facts
- Three memory types (auto-classified):
  - **Semantic**: Facts, knowledge, and information
  - **Episodic**: Personal events and experiences
  - **Procedural**: Processes, preferences, and how-to knowledge
- Memory persistence across sessions (save/load)
- RAG-based memory retrieval
- Deduplication of extracted memories
- `BeforeMemoryStored` event for inspecting extracted facts

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-18 GB depending on model choice)

## How It Works

1. Enable `MemoryExtractionMode.LlmBased` on `AgentMemory`
2. During conversations, the agent automatically extracts facts using grammar-constrained structured output
3. Extracted memories are deduplicated against existing memory before storage
4. When answering, the agent retrieves relevant memories via semantic search
5. Retrieved memories enhance the context for better responses
6. Memory persists to disk and loads on next session

## Usage

1. Run the application
2. Select a language model
3. Chat with the assistant and share information
4. Use `/remember` to explicitly store memories
5. Use `/save` to persist memory to disk
6. Memory auto-loads on restart

## Commands

| Command | Description |
|---------|-------------|
| `/new` | Start a new chat session (clears conversation history, keeps memories) |
| `/remember <info>` | Explicitly store information in memory |
| `/memories` | List all stored memories with timestamps |
| `/capacity <n>` | Set max memory entries (0 = unlimited) |
| `/decay <days>` | Set time-decay half-life in days (0 = off) |
| `/consolidate` | Merge similar memories using LLM summarization |
| `/summarize` | Summarize current conversation into episodic memory |
| `/clear` | Clear all memories (current session) |
| `/save` | Save memories to disk |
| `/load` | Load memories from disk |
| `/help` | Show available commands |
| `quit` | Exit the application (auto-saves) |

## Example Conversations

```
User: My name is Alex and I work as a software engineer at TechCorp.
  [Memory extracted: The user's name is Alex (Semantic, High)]
  [Memory extracted: The user works as a software engineer at TechCorp (Semantic, Medium)]
Assistant: Nice to meet you, Alex! I'll remember that you're a software
engineer at TechCorp. What kind of projects do you work on?

User: I mainly work on backend services using C# and .NET.
  [Memory extracted: The user works on backend services using C# and .NET (Semantic, Medium)]
Assistant: Got it! So you focus on backend development with C# and .NET
at TechCorp. That's a solid tech stack.

User: /new
Started a new chat session. Conversation history cleared, memories preserved.

User: What do you know about me?
Assistant: Based on what I recall, you're Alex, a software engineer
at TechCorp who specializes in backend services using C# and .NET.
```

## Memory Types Explained

| Type | Stores | Examples |
|------|--------|----------|
| **Semantic** | Facts and knowledge | "Alex works at TechCorp", "The project uses PostgreSQL" |
| **Episodic** | Events and experiences | "On Monday we discussed the API design", "User mentioned a deadline" |
| **Procedural** | Preferences and processes | "User prefers detailed explanations", "Always format code in C#" |

## Memory File Location

Memories are saved to: `./agent_memory.bin`

## Architecture

```
+--------------------------------------------------+
|                User Message                       |
+-------------------------+------------------------+
                          |
                          v
+--------------------------------------------------+
|              Memory Retrieval                     |
|    (Semantic search for relevant memories)        |
+-------------------------+------------------------+
                          |
                          v
+--------------------------------------------------+
|              Context Enhancement                  |
|    (Inject relevant memories into prompt)         |
+-------------------------+------------------------+
                          |
                          v
+--------------------------------------------------+
|                Agent Response                     |
+-------------------------+------------------------+
                          |
                          v
+--------------------------------------------------+
|         Automatic Memory Extraction               |
| (LLM-based structured extraction + dedup)         |
+--------------------------------------------------+
```

## Customization

- Set `ExtractionModel` to use a lighter model for extraction
- Set `ExtractionPrompt` to customize extraction guidance
- Subscribe to `BeforeMemoryStored` to filter or modify extracted memories
- Adjust `DeduplicationThreshold` to control duplicate detection sensitivity
- Set `RunExtractionSynchronously` to `false` for non-blocking extraction
- Set `MaxMemoryEntries` and `EvictionPolicy` to control memory growth
- Set `TimeDecayHalfLife` to make recent memories rank higher in retrieval
- Subscribe to `MemoryEvicted` to monitor or prevent evictions
- Call `ConsolidateAsync` to merge similar entries with LLM summarization
- Set `ConsolidationSimilarityThreshold` to control merge aggressiveness
- Subscribe to `BeforeMemoryConsolidated` to inspect or cancel merges
- Call `SummarizeConversationAsync` to capture session highlights as episodic memory
- Set `MaxConversationSummaries` to control how many entries per session (default 3)
