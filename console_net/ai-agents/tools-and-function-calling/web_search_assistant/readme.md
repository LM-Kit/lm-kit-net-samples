# Web Search Assistant

A general-purpose assistant powered by LM-Kit.NET that autonomously decides when to search the web.
Unlike a research agent that always searches, this assistant behaves like ChatGPT: it answers from
its own knowledge for casual or well-known questions, and searches the web only when it needs
fresh, factual, or real-time information.

This is the **reference demo** for integrating the built-in `WebSearchTool` into a conversational assistant.

> **Choose a provider before you run.** The default `BuiltInTools.WebSearch` uses DuckDuckGo's
> public no-JavaScript HTML endpoint. It works for one or two queries but is rate-limited
> aggressively and will start returning blocking errors after a small handful of automated calls
> from the same IP. **For anything beyond a quick local trial, configure an API-backed provider
> (Tavily / Brave / Serper, all have free tiers).** See *Choosing a provider* below.

## Features

- **Autonomous web search**: the LLM decides by itself when to call the web search tool
- **Multiple providers**: DuckDuckGo (no key, dev-only), Brave / Tavily / Serper (production)
- **Full call-flow visibility**: color-coded output shows reasoning, tool calls, and responses
- **Multi-turn conversation**: maintains context across messages
- **Source citation**: the model cites sources when it uses web search results
- **Special commands**: `/reset`, `/regenerate`, `/continue`

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (4-18 GB)
- Internet connectivity for web search

## How It Works

1. A `MultiTurnConversation` is created with a system prompt that instructs the model when to search and when not to
2. The built-in `WebSearchTool` is registered with a single line: `chat.Tools.Register(BuiltInTools.WebSearch)`
3. A `DateTimeTool` is also registered so the model knows the current date
4. When the user asks a question, the LLM internally reasons about whether it needs web data
5. If web search is needed, the model emits a tool invocation (visible in red), receives results, then answers
6. If no search is needed, the model answers directly from its own knowledge
7. The `AfterTextCompletion` event provides streaming visibility into every phase

## Usage

1. Run the application
2. Select a language model (GPT OSS 20B recommended for best tool-calling)
3. Start chatting naturally

### Example Conversations

**Web search triggered:**
```
User: What are the latest headlines today?
  ┌─── Tool Call ──────────────────────────────
  │ web_search({ "query": "latest news headlines today" })
  │ [results from DuckDuckGo...]
  └────────────────────────────────────────────
Assistant: Here are today's top headlines: ...
```

**No web search (general knowledge):**
```
User: Tell me a joke
Assistant: Why did the scarecrow win an award? Because he was outstanding in his field!
```

**No web search (math/logic):**
```
User: What is the square root of 144?
Assistant: The square root of 144 is 12.
```

## Choosing a provider

| Provider | Free quota | API key | Reliability | Recommended for |
|----------|------------|---------|-------------|------------------|
| **Tavily** | 1,000 searches / month | Required ([sign up](https://tavily.com)) | High; AI-optimized snippets | RAG and agent demos, default recommendation |
| **Brave** | 2,000 searches / month | Required ([sign up](https://brave.com/search/api/)) | High | General web search |
| **Serper** | 2,500 searches free, then paid | Required ([sign up](https://serper.dev)) | High; Google-quality | When Google-style ranking matters |
| **SearXNG** | Unlimited | Self-hosted instance | High (your infra) | Privacy-conscious deployments |
| **DuckDuckGo** (default) | None official | None | **Low**: anti-bot blocks after a few queries | Quick local trial only |

When the DuckDuckGo provider is blocked, `WebSearchTool` returns a clear `Error` field in
its JSON response naming the cause and pointing at these alternatives. Your agent should
treat this as a hard failure and ask the user to configure a provider, not retry.

## Tool Registration (Key Code)

```csharp
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Agents.Tools.BuiltIn.Net;

// Create conversation
MultiTurnConversation chat = new(model);

// RECOMMENDED: configure an API-backed provider before registering.
// Tavily: 1,000 free searches per month, AI-optimized for agents.
var webSearch = BuiltInTools.CreateWebSearch(
    WebSearchTool.Provider.Tavily,
    Environment.GetEnvironmentVariable("TAVILY_API_KEY"));
chat.Tools.Register(webSearch);

// Quick local trial only (rate-limited, will block after a few queries):
// chat.Tools.Register(BuiltInTools.WebSearch);

// Register DateTime so the model knows the current date
chat.Tools.Register(BuiltInTools.DateTime);
```

### Other providers

```csharp
// Brave Search (free tier available at https://brave.com/search/api/)
var webSearch = BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Brave, "YOUR_API_KEY");

// Serper (Google results via API, https://serper.dev)
var webSearch = BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Serper, "YOUR_API_KEY");

// Self-hosted SearXNG
var webSearch = new WebSearchTool(new WebSearchTool.Options
{
    SearchProvider = WebSearchTool.Provider.SearXNG,
    BaseUrl = "https://your-searxng-instance.example",
});
```

## Understanding the Output

| Color | Meaning |
|-------|---------|
| **White** | Assistant's response (user-visible text) |
| **Blue** | Internal reasoning (the model's thinking process) |
| **Red** | Tool invocation (web search call and results) |
| **Green** | User/Assistant labels |
| **Gray** | Generation statistics (tokens, speed, context usage) |

The tool call boundaries (`┌─── Tool Call` / `└───`) make it easy to see exactly when
the model decides to search and what results it gets back.

## Agent Configuration

```csharp
MultiTurnConversation chat = new(model)
{
    MaximumCompletionTokens = 2048,
    SamplingMode = new RandomSampling() { Temperature = 0.7f },
    SystemPrompt = @"You are a helpful AI assistant with web search capabilities.
Use web search for current events, factual queries, and real-time data.
Do not use web search for casual conversation, general knowledge, or creative tasks."
};
```

## Customization

- **System prompt**: adjust when the model should and should not search
- **Temperature**: lower for more focused answers, higher for more creative responses
- **MaximumCompletionTokens**: increase for longer responses
- **Web search provider**: switch from DuckDuckGo to Brave/Tavily/Serper for better results
- **Additional tools**: register more built-in tools (Calculator, Http, etc.) to expand capabilities
