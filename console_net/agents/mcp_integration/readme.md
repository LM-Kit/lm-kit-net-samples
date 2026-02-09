# Multi-Turn Chat with MCP

A demo for multi-turn conversations with Model Context Protocol (MCP) server integration using LM-Kit.NET. Connect to external tools and services through MCP-compliant servers.

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
- Support for authenticated MCP servers (e.g., Hugging Face)
- Switch between MCP servers during a session
- Real-time logging of MCP requests and responses

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Choose an MCP server (or enter a custom URI)
4. Chat with the assistant and use the connected tools

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear chat history and start fresh |
| `/regenerate` | Generate a new response to your last input |
| `/continue` | Continue the last assistant response |
| `/server` | Switch to a different MCP server |