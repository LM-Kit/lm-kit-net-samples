# Research Assistant Agent

A demo showcasing an AI research agent using LM-Kit.NET's Agent framework with ReAct (Reasoning + Acting) planning strategy. The agent iteratively researches topics by thinking, acting, and observing results.

## Features

- **ReAct Planning Strategy**: Thought-Action-Observation loops for systematic research
- Real-time display of agent reasoning process
- Web search tool for gathering information
- Note-taking tool to track findings
- Structured report generation with key findings
- Support for multiple LLMs

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-16 GB depending on model choice)

## How It Works

The ReAct pattern enables the agent to:
1. **Think**: Reason about what information is needed
2. **Act**: Use tools (search, take notes) to gather data
3. **Observe**: Analyze tool results and plan next steps
4. **Repeat**: Continue until research is complete

## Usage

1. Run the application
2. Select a language model
3. Enter a research topic or question
4. Watch the agent reason and research in real-time
5. Receive a structured research summary

## Example Topics

```
> Research the benefits and challenges of remote work for software teams
> Compare React, Vue, and Angular for enterprise web applications
> What are the latest trends in sustainable packaging for e-commerce?
> Explain the pros and cons of electric vehicles vs hydrogen fuel cells
```

## Tools Included

| Tool | Description |
|------|-------------|
| `web_search` | Search the web using DuckDuckGo (real live search, no API key required) |
| `take_notes` | Save important findings during research |
| `get_notes` | Retrieve all saved notes |

**Note:** The web search tool performs real HTTP requests to DuckDuckGo and requires an internet connection.

## Agent Configuration

```csharp
var agent = Agent.CreateBuilder(model)
    .WithPersona("Expert Research Analyst")
    .WithPlanning(PlanningStrategy.ReAct)
    .WithTools(tools => {
        tools.Register(new WebSearchTool());
        tools.Register(new NoteTakingTool());
    })
    .WithMaxIterations(10)
    .Build();
```

## Understanding the Output

The demo displays different types of output in different colors:
- **Blue**: Internal reasoning (Thought)
- **Red**: Tool invocations (Action)
- **White**: Final response and observations

## Customization

Adjust research depth by modifying:
- `MaxIterations`: Number of research cycles (default: 10)
- Add custom tools for specialized research domains
- Modify the agent persona for different research styles
