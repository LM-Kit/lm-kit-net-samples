# Local Data Analyst Agent

A demo showcasing an AI-powered data analysis agent that works entirely on your local machine. The agent uses **ReAct planning** with **built-in tools** to read data files, parse structured data, compute statistics, and generate actionable insights, all without sending a single byte to the cloud.

## Features

- **ReAct Planning**: Systematic reasoning about what analyses to perform
- **Built-In Data Tools**: CSV parsing, JSON parsing, XML parsing
- **Built-In Numeric Tools**: Calculator, statistical computations
- **Built-In IO Tools**: File system read, directory listing
- **Privacy-First**: All data stays on your machine
- **Multi-Turn Conversation**: Ask follow-up questions about the same data
- **Color-Coded Output**: Reasoning (blue), tool calls (magenta), results (white)
- **Multiple Model Options**: From 8B to 27B parameters

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (6-18 GB depending on model choice)
- Data files to analyze (CSV, JSON, XML, or text)

## How It Works

The agent uses the ReAct (Reasoning + Acting) pattern to:

1. **Think**: Determine what data to read and what analyses are relevant
2. **Act**: Read files, parse data, compute statistics using built-in tools
3. **Observe**: Analyze tool results and plan further analysis
4. **Synthesize**: Present findings with key metrics and actionable insights

## Usage

1. Run the application
2. Select a language model
3. Point the agent at your data files with natural language
4. Ask questions and get analysis with supporting numbers
5. Ask follow-up questions for deeper insights

## Example Prompts

```
> Analyze the file C:\data\sales_q4.csv and summarize key trends
> List files in C:\data\ and tell me what datasets are available
> Read C:\reports\metrics.json and compute monthly growth rates
> What are the top 5 products by revenue in C:\data\products.csv?
> Compare this quarter's numbers against last quarter
```

## Built-In Tools Used

| Tool | Category | Description |
|------|----------|-------------|
| `filesystem_read` | IO | Read file contents from local disk |
| `filesystem_list` | IO | List directory contents |
| `csv_parse` | Data | Parse CSV into structured rows and columns |
| `json_parse` | Data | Parse JSON documents |
| `xml_parse` | Data | Parse XML documents |
| `calculator` | Numeric | Arithmetic, percentages, ratios |
| `statistics` | Numeric | Mean, median, min, max, standard deviation |

## Configuration

- **Max iterations**: 15 (to handle complex multi-step analyses)
- **Timeout**: 5 minutes per query
- **Planning strategy**: ReAct for systematic reasoning
