# Multi-Turn Chat with Agent Memory

A demo for building AI assistants with long-term memory using LM-Kit.NET. Store facts and information that the assistant can recall during conversations.

## Features

- Agent memory for storing and recalling facts
- Semantic search to find relevant information
- Side-by-side comparison: assistant with vs. without memory
- Persistent memory storage (serialize/deserialize)
- Memory recall events for debugging and logging

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Minimal VRAM (uses Qwen 3 0.6B + embedding model)

## Usage

1. Run the application
2. The demo loads a pre-built memory with customer profile facts
3. Ask questions and compare responses from both assistants
4. The assistant with memory retrieves relevant facts automatically

## How It Works

The demo creates an `AgentMemory` populated with facts:

```csharp
var memory = new AgentMemory(embeddingModel);

await memory.SaveInformationAsync(
    collection: "acmeeCustomerProfile",
    sectionIdentifier: "fact1",
    text: "What is the ideal customer size? -> Between 200 and 500 employees."
);
```

Then attaches it to a conversation:

```csharp
MultiTurnConversation chat = new(model)
{
    Memory = memory
};
```

## Example Questions

- "How many employees do our customers usually have?"
- "In which industries are they working?"
- "What is the typical technology budget?"

## Memory Persistence

Save and reload memory across sessions:

```csharp
// Save
memory.Serialize("memory.bin");

// Load
var memory = AgentMemory.Deserialize("memory.bin", embeddingModel);
```

## Use Cases

- Customer support bots with product knowledge
- Company FAQ assistants
- Domain-specific chatbots with factual recall
- Personal assistants with user preferences