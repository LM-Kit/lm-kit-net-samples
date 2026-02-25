# Agent Skills Demo

Turn a generic LLM into a specialist with a single markdown file.

**Agent Skills** are SKILL.md files that contain expert instructions for a specific task. Load one, activate it, type a short input, and get structured output.

## Two Activation Modes

This demo showcases **two different ways** to activate skills, selectable at startup:

### Mode 1: Manual Activation (SkillActivator + slash commands)

You control which skill is active. Type a slash command like `/explain` to activate a skill, then chat normally. The app uses `SkillActivator` to inject the skill's instructions into each message before sending it to the model. Type `/off` to deactivate.

**Key classes:** `SkillRegistry`, `SkillActivator`, `SkillInjectionMode`

```csharp
// Load skills
var registry = new SkillRegistry();
registry.LoadFromDirectory("./skills");
var activator = new SkillActivator(registry);

// Inject instructions into a message
string instructions = activator.FormatForInjection(skill, SkillInjectionMode.UserMessage);
string prompt = instructions + "\n\n---\n\nUser request: " + userInput;
var result = chat.Submit(prompt);
```

### Mode 2: Model-Driven Activation (SkillTool + function calling)

A `SkillTool` is registered as a function the model can call. The model reads the tool description, discovers available skills, and activates them autonomously. No slash commands needed: just describe what you need in plain language.

**Key classes:** `SkillRegistry`, `SkillTool`

```csharp
// Load skills and register the tool
var registry = new SkillRegistry();
registry.LoadFromDirectory("./skills");
chat.Tools.Register(new SkillTool(registry));

// The model can now call activate_skill on its own
var result = chat.Submit("explain what blockchain is");
```

### When to Use Which

| Criteria | Manual (SkillActivator) | Model-Driven (SkillTool) |
|----------|------------------------|---------------------------|
| User controls activation | Yes, via slash commands | No, model decides |
| Requires function calling | No | Yes |
| Best for | Predictable apps, menus | Autonomous agents |
| Skill discovery | User picks from a list | Model reads tool description |


## What This Demo Does

Three bundled skills, each works from a single line of input:

| Command | You type | You get |
|---------|----------|----------|
| /explain | "blockchain" | Clear, jargon-free explanation with analogy and example |
| /pros-cons | "electric cars" | Balanced table of pros, cons, and a bottom line |
| /email-writer | "thank a vendor for fast delivery" | Complete professional email with subject line |


## How to Run

```bash
cd demos/console_net/agents/skill_based_assistant
dotnet run
```

1. Pick a model (Gemma 3 12B recommended, ~9 GB VRAM).
2. Choose an activation mode (Manual or Model-driven).
3. Chat using the selected mode.


## Example Session: Manual Activation

```
=== Skill Activation Mode ===

LM-Kit supports two ways to activate skills:

  1 - Manual activation (SkillActivator + slash commands)
  2 - Model-driven activation (SkillTool + function calling)

> 1

Agent Skills Demo
=================

Mode: Manual activation (SkillActivator + slash commands)

You: /explain
Skill activated: explain

You: blockchain

Assistant:
## Blockchain

**In one sentence:** A blockchain is a shared digital ledger that records
transactions in a way no single person can alter.

**How it works:** Imagine a notebook that thousands of people have identical
copies of. Every time someone writes a new entry, everyone's copy updates
automatically, and no one can erase old entries.

**Why it matters:** It lets strangers trust each other without a middleman
like a bank.

**Example:** Bitcoin uses a blockchain to track who owns which coins.
```

## Example Session: Model-Driven Activation

```
=== Skill Activation Mode ===

LM-Kit supports two ways to activate skills:

  1 - Manual activation (SkillActivator + slash commands)
  2 - Model-driven activation (SkillTool + function calling)

> 2

Agent Skills Demo
=================

Mode: Model-driven activation (SkillTool + function calling)

The model has access to an activate_skill tool and can discover
skills on its own. Just describe what you need.

You: explain what blockchain is

[SkillTool] Model activated skill: explain

Assistant:
## Blockchain

**In one sentence:** A blockchain is a shared digital ledger that records
transactions in a way no single person can alter.
...
```

## Commands (Manual Mode Only)

| Command | Description |
|---------|-------------|
| /explain | Activate the plain-language explainer |
| /pros-cons | Activate the pros and cons analyst |
| /email-writer | Activate the email writer |
| /off | Deactivate the current skill |
| /skills | List all available skills |
| /help | Show available commands |


## How Skills Work

A skill is a folder with a SKILL.md file containing YAML metadata and markdown instructions:

```
skills/
  explain/
    SKILL.md          <-- name, description, instructions
  pros-cons/
    SKILL.md
  email-writer/
    SKILL.md
```

In **manual mode**, when you activate a skill, its instructions are injected into the conversation using `SkillActivator`. The model follows them until you deactivate or switch.

In **model-driven mode**, a `SkillTool` exposes an `activate_skill` function to the model. The model calls it when it determines a skill would help answer the user's request.


## Creating Your Own Skill

1. Create a folder under skills/ (e.g. skills/my-skill/).
2. Add a SKILL.md file with name, description, and instructions.
3. Restart the demo. Your skill appears automatically.


## Prerequisites

- .NET 8.0 or later
- 4 to 18 GB VRAM depending on the model


## Learn More

- [LM-Kit.NET Documentation](https://docs.lm-kit.com/lm-kit-net/)

- [Agent Skills Specification](https://agentskills.io)

- [How-To: Add Skills to Your AI Assistant](https://docs.lm-kit.com/lm-kit-net/guides/how-to/add-skills-to-assistant.html)
