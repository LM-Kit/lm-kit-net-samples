# Streaming Agent Responses

Interactive console app showcasing three streaming patterns on the same agent: `IAsyncEnumerable<AgentStreamToken>`, the callback-based `IAgentStreamHandler`, and a streaming-to-file recipe.

## What it shows

- `StreamingAgentExecutor` with `BufferSize`, `StreamThinking`, `StreamToolCalls` toggles.
- `StreamAsync(agent, input)` for the pull-based `await foreach` pattern.
- `ExecuteStreamingAsync(agent, input, IAgentStreamHandler)` for the push-based pattern.
- `AgentStreamToken.Type` enum: `Content`, `Thinking`, `ToolCall`, `ToolResult`.
- Three interactive modes from a menu:
  - **Stream**: `IAsyncEnumerable` with typed tokens rendered in color.
  - **Callback**: bundled `DelegateStreamHandler.Console()` handler.
  - **Save**: stream simultaneously to console and a UTF-8 text file.

## Run

```bash
cd console_net/ai-agents/streaming/agent_streaming
dotnet run
```

No command-line arguments. The model loads once at startup.

## Where this fits

A chat UI that waits for full inference is a dead chat UI. Streaming tokens, reasoning, and tool calls are how production apps feel responsive. The save mode also shows the same primitive being used to drive log files or downstream pipelines.
