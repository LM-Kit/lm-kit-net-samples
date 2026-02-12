# Smart Task Router

A demo showcasing intelligent task delegation using LM-Kit.NET's SupervisorOrchestrator. A supervisor agent analyzes incoming requests and delegates to the most appropriate specialist worker.

## Features

- **SupervisorOrchestrator**: Dynamic task routing via supervisor agent
- Intelligent request classification
- Specialized worker agents:
  - **Code Expert**: Programming and software development
  - **Data Analyst**: Data analysis and visualization
  - **Writer**: Content creation and documentation
  - **Researcher**: Information gathering and synthesis
- Delegation reasoning visible in real-time
- Multi-step task handling with context passing

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-16 GB depending on model choice)

## How It Works

1. User submits a request (can be complex or multi-faceted)
2. Supervisor agent analyzes the request
3. Supervisor decides which worker(s) should handle it
4. Supervisor delegates via the DelegateTool
5. Worker completes their portion
6. Supervisor may delegate to additional workers if needed
7. Final response is assembled and returned

## Usage

1. Run the application
2. Select a language model
3. Enter any request - simple or complex
4. Watch the supervisor analyze and delegate
5. See specialists handle their portions
6. Receive the combined result

## Example Requests

```
> Write a Python function to sort a list and explain how it works
> Analyze this sales data and create a summary report: Q1: $50K, Q2: $75K, Q3: $60K, Q4: $90K
> Research best practices for REST API design and write documentation for our team
> Help me understand machine learning - I need both theory and a code example
```

## Worker Specializations

| Worker | Expertise | Handles |
|--------|-----------|---------|
| **Code Expert** | Programming, debugging, code review | Code writing, bug fixes, refactoring |
| **Data Analyst** | Statistics, trends, visualization | Data analysis, metrics, reporting |
| **Writer** | Documentation, content, communication | Articles, docs, explanations |
| **Researcher** | Information synthesis, learning | Research, comparisons, deep dives |

## Understanding the Output

The demo displays:
- Supervisor's reasoning about task routing (blue)
- Delegation decisions (yellow)
- Worker responses (green)
- Final assembled response (white)

## Architecture

```
                    ┌─────────────────┐
                    │   User Input    │
                    └────────┬────────┘
                             │
                    ┌────────▼────────┐
                    │   Supervisor    │
                    │     Agent       │
                    │  (Coordinator)  │
                    └────────┬────────┘
                             │
            ┌────────────────┼────────────────┐
            │      DelegateTool Routes        │
            │        Based on Task            │
            └────────────────┬────────────────┘
                             │
    ┌────────────┬───────────┼───────────┬────────────┐
    │            │           │           │            │
    ▼            ▼           ▼           ▼            │
┌────────┐  ┌────────┐  ┌────────┐  ┌────────┐        │
│  Code  │  │  Data  │  │ Writer │  │Research│        │
│ Expert │  │Analyst │  │        │  │   er   │        │
└────┬───┘  └────┬───┘  └────┬───┘  └────┬───┘        │
     │           │           │           │            │
     └───────────┴───────────┴───────────┘            │
                             │                        │
                    ┌────────▼────────┐               │
                    │   Supervisor    │◀──────────────┘
                    │   Assembles     │  (may delegate again)
                    │    Response     │
                    └─────────────────┘
```

## Customization

- Add more worker specialists for domain-specific expertise
- Adjust supervisor instructions for different routing strategies
- Configure delegation limits to control depth
- Add tools to workers for specialized capabilities
