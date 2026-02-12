# Multi-Turn Chat with MCP (Full Protocol)

A demo for multi-turn conversations with Model Context Protocol (MCP) server integration using LM-Kit.NET. Showcases the complete MCP protocol surface: tools, resources, sampling, roots, elicitation, progress tracking, logging, resource subscriptions, and cancellation handling.

## Features

- Multi-turn chat with tool calling via MCP
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Pre-configured public MCP servers:
  - DeepWiki (query GitHub repos)
  - Microsoft Learn Docs
  - Currency conversion
  - Time & timezone utilities
  - Domain search & WHOIS
  - Text extraction from URLs
  - Everything (reference server with resources/prompts/tools)
- Support for authenticated MCP servers (e.g., Hugging Face)
- Switch between MCP servers during a session
- Real-time logging of MCP requests and responses

### New MCP Protocol Features

- **Sampling**: Server can request LLM completions from the client (fulfilled by the loaded model)
- **Elicitation**: Server can request structured user input via console prompts
- **Roots**: Manage filesystem boundaries exposed to MCP servers
- **Progress tracking**: Real-time progress display for long-running server operations
- **Logging**: Structured server-side log messages with configurable log level
- **Resource browsing**: List resources and resource templates from the server
- **Resource subscriptions**: Subscribe to resources and receive real-time update notifications
- **Cancellation**: Handle server-initiated request cancellation
- **Capability detection**: Display server capabilities after connection

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3-18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Choose an MCP server (or enter a custom URI)
4. Chat with the assistant and use the connected tools
5. Use slash commands to explore advanced MCP protocol features

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear chat history and start fresh |
| `/regenerate` | Generate a new response to your last input |
| `/continue` | Continue the last assistant response |
| `/server` | Switch to a different MCP server |
| `/resources` | Browse resources, templates, and subscribe to updates |
| `/capabilities` | View server capability flags and session info |
| `/roots` | View and manage filesystem roots |
| `/loglevel` | Set the server-side minimum log level |

## MCP Protocol Features Demonstrated

### Sampling (Server-to-Client LLM Requests)

When an MCP server needs an LLM completion, it sends a sampling request. The demo uses the loaded model to fulfill these requests, including system prompts and token limits.

### Elicitation (Server-Requested User Input)

When a server needs structured user input (e.g., confirmation, parameters), the demo prompts the user interactively and returns the response.

### Roots (Filesystem Boundaries)

Roots tell the server which filesystem paths the client makes available. The demo adds the working directory by default and lets you add more via `/roots`.

### Progress Tracking

Long-running server operations emit progress notifications. The demo displays these as percentage or step counters in real time.

### Logging

Servers can emit structured log messages. The demo color-codes them by severity. Use `/loglevel` to adjust the minimum level the server sends.

### Resource Subscriptions

Use `/resources` to browse the server's resource catalog and subscribe to specific URIs. When a subscribed resource changes, the demo displays a notification.
