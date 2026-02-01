# Persistent Memory Assistant

A demo showcasing LM-Kit.NET's AgentMemory system for building AI assistants that remember context across conversations. The agent stores and recalls information using RAG-based semantic memory.

## Features

- **AgentMemory**: Persistent semantic memory storage
- Three memory types:
  - **Semantic**: Facts, knowledge, and information
  - **Episodic**: Personal events and experiences
  - **Procedural**: Processes, preferences, and how-to knowledge
- Memory persistence across sessions (save/load)
- RAG-based memory retrieval
- Automatic context enhancement from memory

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-16 GB depending on model choice)

## How It Works

1. During conversations, the agent stores relevant information in memory
2. Memory is categorized by type (semantic, episodic, procedural)
3. When answering, the agent retrieves relevant memories via semantic search
4. Retrieved memories enhance the context for better responses
5. Memory persists to disk and loads on next session

## Usage

1. Run the application
2. Select a language model
3. Chat with the assistant and share information
4. Use `/remember` to explicitly store memories
5. Use `/recall` to search your memory
6. Use `/save` to persist memory to disk
7. Memory auto-loads on restart

## Commands

| Command | Description |
|---------|-------------|
| `/remember <info>` | Explicitly store information in memory |
| `/recall <query>` | Search memory for relevant information |
| `/memories` | List all stored memories |
| `/clear` | Clear all memories (current session) |
| `/save` | Save memories to disk |
| `/load` | Load memories from disk |
| `quit` | Exit the application |

## Example Conversations

```
User: My name is Alex and I work as a software engineer at TechCorp.
Assistant: Nice to meet you, Alex! I'll remember that you're a software
engineer at TechCorp. What kind of projects do you work on?

User: I mainly work on backend services using C# and .NET.
Assistant: Got it! So you focus on backend development with C# and .NET
at TechCorp. That's a solid tech stack.

[Later or in a new session]

User: What do you know about me?
Assistant: Based on what you've told me, you're Alex, a software engineer
at TechCorp who specializes in backend services using C# and .NET.
```

## Memory Types Explained

| Type | Stores | Examples |
|------|--------|----------|
| **Semantic** | Facts and knowledge | "Alex works at TechCorp", "The project uses PostgreSQL" |
| **Episodic** | Events and experiences | "On Monday we discussed the API design", "User mentioned a deadline" |
| **Procedural** | Preferences and processes | "User prefers detailed explanations", "Always format code in C#" |

## Memory File Location

Memories are saved to: `./agent_memory.json`

## Architecture

```
┌─────────────────────────────────────────────────┐
│                User Message                      │
└─────────────────────┬───────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              Memory Retrieval                    │
│    (Semantic search for relevant memories)       │
└─────────────────────┬───────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              Context Enhancement                 │
│    (Inject relevant memories into prompt)        │
└─────────────────────┬───────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│                Agent Response                    │
└─────────────────────┬───────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────┐
│              Memory Storage                      │
│    (Store new facts from conversation)           │
└─────────────────────────────────────────────────┘
```

## Customization

- Adjust memory retrieval count for more/fewer context items
- Configure memory importance thresholds
- Customize memory extraction prompts
- Add domain-specific memory categories
