# Persona-Driven Chatbot

Minimal chat agent built via `AgentTemplates.Chat(model).WithPersonality(...).Build()`. A stable system prompt, a name, and a loop.

## What it shows

- `AgentTemplates.Chat(LM)` factory.
- `.WithPersonality(string)` fluent builder method.
- `Agent.Run(input)` synchronous execution returning `AgentExecutionResult`.
- `AgentExecutionResult.Content`, `.IsSuccess`, `.Duration`, `.InferenceCount` for the result surface.

## Run

```bash
cd console_net/ai-agents/chatbots/chatbots
dotnet run                                # default support persona
dotnet run -- "You are a strict reviewer..." # pass a custom persona
```

## Where this fits

Real apps need persona-stable chatbots, not raw `MultiTurnConversation`s. The template hides system-prompt plumbing and gives you one place to swap personas.
