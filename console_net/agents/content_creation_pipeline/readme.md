# Content Creation Pipeline

A demo showcasing sequential multi-agent orchestration using LM-Kit.NET's PipelineOrchestrator. Content flows through a series of specialized agents, each refining and improving it.

## Features

- **PipelineOrchestrator**: Sequential agent chaining
- Four-stage content pipeline:
  - **Outliner**: Creates structured outline from topic
  - **Writer**: Expands outline into full content
  - **Editor**: Refines grammar, style, and clarity
  - **Fact-Checker**: Verifies claims and adds caveats
- Progress visualization showing content evolution
- Intermediate output at each stage

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-18 GB depending on model choice)

## How It Works

```
Topic/Brief → Outliner → Writer → Editor → Fact-Checker → Final Content
```

1. **Outliner Agent**: Analyzes the topic and creates a structured outline with sections and key points
2. **Writer Agent**: Takes the outline and expands it into full prose content
3. **Editor Agent**: Improves readability, fixes grammar, and enhances flow
4. **Fact-Checker Agent**: Reviews claims, adds appropriate caveats, and ensures accuracy

## Usage

1. Run the application
2. Select a language model
3. Enter a topic or brief for content creation
4. Watch the content evolve through each pipeline stage
5. Receive polished, fact-checked content

## Example Topics

```
> Write a blog post about the benefits of TypeScript for large-scale applications
> Create an article explaining how blockchain technology works for beginners
> Write a guide on best practices for remote team management
> Create content about sustainable living tips for apartment dwellers
```

## Pipeline Stages

| Stage | Agent | Input | Output |
|-------|-------|-------|--------|
| 1 | Outliner | Topic/Brief | Structured outline |
| 2 | Writer | Outline | Draft content |
| 3 | Editor | Draft | Polished content |
| 4 | Fact-Checker | Polished content | Verified final content |

## Understanding the Output

The demo displays:
- Each stage's output as it completes
- Visual indication of pipeline progress
- Final aggregated content
- Statistics about the pipeline execution

## Architecture

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│  Outliner   │ ──▶ │   Writer    │ ──▶ │   Editor    │ ──▶ │Fact-Checker │
│   Agent     │     │   Agent     │     │   Agent     │     │   Agent     │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
      │                   │                   │                   │
      ▼                   ▼                   ▼                   ▼
  Outline            Draft Text         Polished Text       Final Content
```

## Customization

- Add or remove pipeline stages based on your workflow
- Customize agent personas for different content types (technical, marketing, etc.)
- Adjust prompts for different output formats (blog, documentation, social media)
- Use `StopOnFailure` option to halt pipeline on errors
