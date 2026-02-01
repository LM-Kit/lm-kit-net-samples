# Multi-Agent Document Review

A demo showcasing parallel multi-agent orchestration using LM-Kit.NET. Multiple specialized agents review the same document simultaneously, each from their unique expert perspective.

## Features

- **ParallelOrchestrator**: Run multiple agents concurrently
- Three specialized reviewer agents:
  - **Technical Reviewer**: Evaluates technical accuracy and feasibility
  - **Business Analyst**: Assesses business impact and ROI
  - **Compliance Reviewer**: Checks for risks and regulatory concerns
- Aggregated findings from all perspectives
- Consensus and disagreement highlighting
- Real-time progress display

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-16 GB depending on model choice)

## How It Works

1. User provides a document or proposal to review
2. ParallelOrchestrator dispatches the document to all three agents simultaneously
3. Each agent analyzes from their specialized perspective
4. Results are aggregated into a comprehensive review report
5. Common themes and conflicting opinions are highlighted

## Usage

1. Run the application
2. Select a language model
3. Enter or paste the document/proposal to review
4. Watch all three agents work in parallel
5. Receive a unified review with multiple perspectives

## Example Documents to Review

```
> We propose migrating our monolithic application to microservices architecture.
  This will require 6 months of development effort and $500K budget. Benefits
  include independent scaling, faster deployments, and technology flexibility.

> Our team recommends adopting AI-powered customer service chatbots to reduce
  support tickets by 40%. Implementation requires integration with our CRM
  and training on historical ticket data.
```

## Agent Perspectives

| Agent | Focus Areas |
|-------|-------------|
| **Technical Reviewer** | Architecture, scalability, security, implementation complexity |
| **Business Analyst** | ROI, market impact, resource requirements, timeline |
| **Compliance Reviewer** | Legal risks, data privacy, regulatory requirements, security compliance |

## Understanding the Output

The demo displays:
- Individual reviews from each specialist (color-coded)
- An aggregated summary highlighting:
  - Points of agreement across reviewers
  - Conflicting opinions or concerns
  - Overall recommendation

## Architecture

```
                    ┌─────────────────┐
                    │   User Input    │
                    │   (Document)    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │    Parallel     │
                    │  Orchestrator   │
                    └────────┬────────┘
                             │
         ┌───────────────────┼───────────────────┐
         │                   │                   │
         ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│    Technical    │ │    Business     │ │   Compliance    │
│    Reviewer     │ │    Analyst      │ │    Reviewer     │
└────────┬────────┘ └────────┬────────┘ └────────┬────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                    ┌────────▼────────┐
                    │   Aggregator    │
                    │   (Summary)     │
                    └─────────────────┘
```

## Customization

- Add more reviewer perspectives by creating additional agents
- Adjust `MaxParallelism` to control concurrent execution
- Customize aggregation logic for different review types
- Modify agent personas for domain-specific reviews
