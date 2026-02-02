# MCP Stdio Transport Integration Demo

This demo showcases how to integrate local MCP (Model Context Protocol) servers with LM-Kit.NET using the **stdio transport**. The stdio transport is the standard method for communicating with local MCP servers, where the server runs as a subprocess and communication happens via stdin/stdout using JSON-RPC 2.0 messages.

## Features

- **Stdio Transport**: Spawn and communicate with local MCP servers as subprocesses
- **Multiple Server Types**: Connect to Node.js (npx) or Python (uvx) based MCP servers
- **Real-time Communication**: JSON-RPC 2.0 over newline-delimited JSON (JSONL)
- **Process Lifecycle Management**: Automatic startup, monitoring, and graceful shutdown
- **Event Handling**: Server notifications, stderr logging, and disconnection events
- **Fluent Builder API**: Easy configuration with `McpClientBuilder`

## Prerequisites

- .NET 8.0 or later
- For Node.js servers: Node.js 18+ with npx
- For Python servers: Python 3.10+ with uvx (from uv package manager)
- Sufficient VRAM for the selected language model (3-6 GB depending on model)

## Available MCP Servers

### Node.js Servers (via npx)

| Server | Description | Package |
|--------|-------------|---------|
| Filesystem | Read/write files in a directory | `@modelcontextprotocol/server-filesystem` |
| Memory | In-memory key-value storage | `@modelcontextprotocol/server-memory` |
| Fetch | Make HTTP requests | `@modelcontextprotocol/server-fetch` |

### Python Servers (via uvx)

| Server | Description | Package |
|--------|-------------|---------|
| Git | Git repository operations | `mcp-server-git` |
| SQLite | SQLite database operations | `mcp-server-sqlite` |

## How It Works

### 1. Transport Architecture

```
┌─────────────────┐       stdin (JSON-RPC)      ┌─────────────────┐
│                 │ ─────────────────────────▶  │                 │
│   LM-Kit.NET    │                             │   MCP Server    │
│   McpClient     │ ◀─────────────────────────  │   (subprocess)  │
│                 │       stdout (JSON-RPC)     │                 │
└─────────────────┘                             └─────────────────┘
                          stderr (logs)
                   ◀─────────────────────────
```

### 2. Message Format

Messages are newline-delimited JSON (JSONL):

```json
{"jsonrpc":"2.0","id":1,"method":"initialize","params":{...}}
{"jsonrpc":"2.0","id":1,"result":{...}}
```

### 3. Usage Patterns

**Simple Creation:**
```csharp
// Quick stdio client
var client = McpClient.ForStdio("npx", "@modelcontextprotocol/server-filesystem /tmp");
```

**Builder Pattern:**
```csharp
var client = McpClientBuilder.ForStdio("npx", "@modelcontextprotocol/server-filesystem")
    .WithWorkingDirectory("/path/to/dir")
    .WithEnvironment("DEBUG", "true")
    .WithStderrHandler(line => Console.WriteLine($"[Server] {line}"))
    .WithRequestTimeout(TimeSpan.FromMinutes(2))
    .WithAutoRestart(maxAttempts: 3)
    .Build();
```

**Detailed Options:**
```csharp
var options = new StdioTransportOptions
{
    Command = "python",
    Arguments = "-m my_mcp_server --verbose",
    WorkingDirectory = "/path/to/server",
    Environment = new Dictionary<string, string>
    {
        ["API_KEY"] = "your-key",
        ["DEBUG"] = "true"
    },
    RequestTimeout = TimeSpan.FromSeconds(60),
    StderrHandler = line => Console.WriteLine(line),
    GracefulShutdown = true,
    AutoRestart = true,
    MaxRestartAttempts = 3
};

var client = McpClient.ForStdio(options);
```

## Running the Demo

1. Ensure you have Node.js or Python installed (depending on which server you want to use)

2. Run the demo:
   ```bash
   dotnet run
   ```

3. Select a language model when prompted

4. Select an MCP server type:
   - For filesystem operations, choose option 0
   - For memory storage, choose option 1
   - For custom servers, choose option 5

5. Interact with the assistant. Example prompts:
   - "List the files in the current directory"
   - "Read the contents of README.md"
   - "Create a new file called test.txt with some content"

## Example Session

```
=== Stdio MCP Server Selection ===

Select a local MCP server to connect via stdio transport:

--- Node.js MCP Servers (requires Node.js/npx) ---
0 - Filesystem Server - Read/write files in a directory

> 0

Enter the directory path to allow access to (or press Enter for current directory):
> /home/user/projects

Starting filesystem MCP server for: /home/user/projects
Command: npx @modelcontextprotocol/server-filesystem "/home/user/projects"

[Server] MCP server started
[MCP Sending] initialize | {"protocolVersion":"2025-06-18",...}
[MCP Received] Method: initialize, Status: 200, Body: {"result":{"protocolVersion":"2025-06-18",...