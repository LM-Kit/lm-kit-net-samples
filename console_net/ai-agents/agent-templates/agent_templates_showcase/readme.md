# Agent Templates Showcase

Interactive console app that demonstrates the four pre-configured agent templates: Chat, Writer, Analyst, ReAct. Run one prompt across all four side-by-side, or open a REPL with any single template.

## What it shows

- `AgentTemplates.Chat(model)`, `Writer(model)`, `Analyst(model)`, `ReAct(model)` factories.
- Each template returns a typed builder you can further customise (`.WithPersonality`, `.WithTone`, `.WithContentType`, etc.) before calling `.Build()`.
- `AgentExecutionResult.Status`, `InferenceCount`, and `Duration` reported per run.
- Five interactive modes from a menu:
  - **Compare**: run one prompt across all 4 templates side-by-side.
  - **Chat / Writer / Analyst / ReAct**: open a REPL with that single template.

## Run

```bash
cd console_net/ai-agents/agent-templates/agent_templates_showcase
dotnet run
```

No command-line arguments. The model loads once at startup.

## Where this fits

You should not be hand-crafting system prompts for every new agent. The templates ship proven scaffolds: ReAct's reasoning hooks, Analyst's structured-thinking prompt, Writer's stylistic levers, Chat's lightweight defaults.
