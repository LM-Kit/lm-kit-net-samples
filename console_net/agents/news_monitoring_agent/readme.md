# News Monitoring Agent

An AI-powered news monitoring agent that uses LM-Kit.NET's built-in `RssFeedTool` and `WebSearchTool` to fetch, search, and summarize news from RSS/Atom feeds. The agent can provide news briefings, search for specific topics across feeds, and supplement coverage with web search results.

## Features

- **RSS/Atom feed monitoring**: Fetch and parse feeds in RSS 2.0, Atom, and RSS 1.0 (RDF) formats
- **Keyword search**: Search feed entries by keyword or date range
- **Web search augmentation**: Supplement RSS results with live web search via DuckDuckGo
- **Multi-feed analysis**: Compare coverage of the same topic across different sources
- **Quick briefings**: Get summarized news briefings from all monitored feeds
- **Configurable feeds**: Select from preset feeds or add custom URLs

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3-18 GB depending on model choice)
- Internet connectivity (for RSS feeds and web search)

## How It Works

1. User selects a language model and RSS feeds to monitor
2. The agent is configured with `RssFeedTool`, `WebSearchTool`, and `DateTimeTool`
3. User asks questions about the news or requests briefings
4. The agent fetches feeds, searches for relevant entries, and summarizes findings
5. Web search supplements feed results when needed

## Built-In Feeds

| # | Feed | Description |
|---|------|-------------|
| 0 | Hacker News | Technology and startup news |
| 1 | TechCrunch | Technology industry coverage |
| 2 | Ars Technica | Technology, science, and culture |
| 3 | The Verge | Technology and entertainment |
| 4 | BBC News - World | Global news coverage |
| 5 | Reuters - World | International wire service |
| 6 | Custom | Enter your own feed URL |

## Usage

1. Run the application
2. Select a language model
3. Choose feeds to monitor (comma-separated numbers, or Enter for all)
4. Ask questions or use commands

## Commands

| Command | Description |
|---------|-------------|
| `/briefing` | Quick news briefing from all monitored feeds |
| `/feeds` | Show monitored feed URLs |
| `/reset` | Clear chat history |

## Example Queries

```
> What's the latest news about AI?
> Give me a briefing from Hacker News and TechCrunch
> Search for articles about climate change from the last week
> Compare how BBC and Reuters are covering the latest tech story
> What's trending in technology today?
```

## Architecture

```
┌─────────────────┐
│   User Input    │
└────────┬────────┘
         │
┌────────▼────────┐
│   News Agent    │
│  (MultiTurn     │
│   Conversation) │
└────────┬────────┘
         │
    ┌────┴────┐
    │  Tools  │
    ├─────────┤
    │ RssFeed │──── Fetch / Search / Parse RSS & Atom feeds
    ├─────────┤
    │WebSearch│──── DuckDuckGo web search for additional context
    ├─────────┤
    │DateTime │──── Current date/time for relative date queries
    └─────────┘
```

## Customization

- Add more RSS feed URLs to the preset list
- Switch `WebSearchTool` provider to Brave, Tavily, or Serper for higher quality results
- Adjust `MaxEntries` on `RssFeedToolOptions` to control how many entries are fetched
- Change `Temperature` for more or less creative summaries
