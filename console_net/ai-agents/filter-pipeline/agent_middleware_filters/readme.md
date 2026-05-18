# Filter Pipeline Demo

Demonstrates how to use LM-Kit.NET's **FilterPipeline** to attach middleware-style filters that intercept the prompt, completion, and tool invocation stages of text generation.

## Features

- **Prompt Filters**: log, rewrite, or short-circuit prompts before inference
- **Completion Filters**: inspect results, collect telemetry, enforce quality gates
- **Tool Invocation Filters**: log, rate-limit, cache, cancel, or override tool calls
- **Cross-filter state**: share data between prompt, completion, and tool filters via `Properties`
- **Lambda and class-based filters**: both inline delegates and reusable `IPromptFilter` / `ICompletionFilter` / `IToolInvocationFilter` classes

## Prerequisites

- .NET 8.0 or later
- VRAM for selected model (6 to 18 GB depending on choice)

## How It Works

The demo has three parts:

1. **Prompt & Completion Filters**: attaches logging, prompt rewriting, and telemetry filters to a `MultiTurnConversation`.
2. **Tool Invocation Filters**: attaches logging, rate-limiting, and caching filters to an `Agent` via `AgentBuilder.WithFilters()`.
3. **Interactive Chat**: runs a multi-turn chat session with all three filter types active, showing turn timing, tool logging, and session statistics.

## Usage

```bash
cd demos/console_net/agents/filter_pipeline
dotnet build
dotnet run
```

## Example Output

```
  [PROMPT FILTER: Logger]
    Input prompt: "What are the benefits of middleware patterns?"
  [PROMPT FILTER: Rewriter]
    Added brevity constraint to prompt