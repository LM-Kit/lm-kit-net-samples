# Agent Skills Demo

Turn a generic LLM into a specialist with a single markdown file.

**Agent Skills** are SKILL.md files that contain expert instructions for a specific task. Load one, activate it with a slash command, type a short input, and get structured output.

## What This Demo Does

Three bundled skills, each works from a single line of input:

| Command | You type | You get |
|---------|----------|---------|
| /explain | "blockchain" | Clear, jargon-free explanation with analogy and example |
| /pros-cons | "electric cars" | Balanced table of pros, cons, and a bottom line |
| /email-writer | "thank a vendor for fast delivery" | Complete professional email with subject line |

## How to Run

```bash
cd demos/console_net/agents/skill_based_assistant
dotnet run
```

1. Pick a model (Gemma 3 12B recommended, ~9 GB VRAM).
2. Type /explain to activate a skill.
3. Type: blockchain
4. Get a structured explanation.
5. Switch directly: type /pros-cons, then /email-writer. No need to /off first.

## Example Session

```
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


You: /pros-cons
Skill activated: pros-cons

You: remote work

Assistant:
## Remote Work

### Pros
- **No commute:** Saves 5-10 hours per week for the average worker.
- **Flexible schedule:** Easier to handle appointments and family needs.
- **Wider talent pool:** Companies can hire from anywhere.

### Cons
- **Isolation:** Less spontaneous interaction with colleagues.
- **Work-life blur:** Harder to disconnect when your office is your home.
- **Communication overhead:** More meetings and messages to stay aligned.

### Bottom Line
Works well for self-motivated people in roles that do not require physical
presence. Less suited for roles that depend on real-time collaboration.


You: /email-writer
Skill activated: email-writer

You: thank a vendor for fast delivery

Assistant:
Subject: Thank You for the Fast Delivery

Dear [Vendor Name],

I wanted to thank you for the prompt delivery of our recent order.
The shipment arrived two days ahead of schedule, which helped us
stay on track with our project timeline.

We look forward to continuing our partnership on future orders.

Best regards,
[Your Name]
```

## Commands

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

When you activate a skill, its instructions are injected into the conversation. The model follows them until you deactivate or switch to another skill.

## Creating Your Own Skill

1. Create a folder under skills/ (e.g. skills/my-skill/).
2. Add a SKILL.md file with name, description, and instructions.
3. Restart the demo. Your skill appears automatically.

## Prerequisites

- .NET 8.0 or later
- 4 to 16 GB VRAM depending on the model

## Learn More

- [LM-Kit.NET Documentation](https://docs.lm-kit.com/lm-kit-net/)
- [Agent Skills Specification](https://agentskills.io)
- [How-To: Add Skills to Your AI Assistant](https://docs.lm-kit.com/lm-kit-net/guides/how-to/add-skills-to-assistant.html)
