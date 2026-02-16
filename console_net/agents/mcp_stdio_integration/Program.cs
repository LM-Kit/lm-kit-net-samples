using LMKit.Mcp.Client;
using LMKit.Mcp.Transport;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.Json;

namespace mcp_stdio_integration
{
    /// <summary>
    /// Demonstrates MCP integration using stdio transport with local MCP servers.
    /// This example shows how to connect to MCP servers running as subprocesses
    /// using stdin/stdout communication (the standard transport for local servers).
    /// </summary>
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_12B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
        static readonly string DEFAULT_GLM_4_7_FLASH_MODEL_PATH = @"https://huggingface.co/lm-kit/glm-4.7-flash-gguf/resolve/main/GLM-4.7-Flash-64x2.6B-Q4_K_M.gguf";

        static bool _isDownloading;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write("\rDownloading model {0:0.00}%", progressPercentage);
            }
            else
            {
                Console.Write("\rDownloading model {0} bytes", bytesRead);
            }

            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write("\rLoading model {0}%", Math.Round(progress * 100));
            return true;
        }

        private static void Main(string[] args)
        {
            // Set your license key here if you have one
            // LMKit.Licensing.LicenseManager.SetLicenseKey("YOUR_LICENSE_KEY");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = UTF8Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== MCP Stdio Transport Demo ===\n");
            Console.WriteLine("This demo shows how to connect to local MCP servers using stdio transport.");
            Console.WriteLine("The stdio transport spawns a subprocess and communicates via stdin/stdout.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("1 - Google Gemma 3 12B Medium (requires approximately 9 GB of VRAM)");
            Console.WriteLine("2 - Microsoft Phi-4 Mini 3.82B (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("3 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("4 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink;

            switch (input?.Trim())
            {
                case "0": modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH; break;
                case "1": modelLink = DEFAULT_GEMMA3_12B_MODEL_PATH; break;
                case "2": modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH; break;
                case "3": modelLink = DEFAULT_QWEN3_8B_MODEL_PATH; break;
                case "4": modelLink = DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH; break;
                case "5": modelLink = DEFAULT_GLM_4_7_FLASH_MODEL_PATH; break;
                default: modelLink = input!.Trim().Trim('"'); break;
            }

            Uri modelUri = new(modelLink);
            LM model = new(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();

            MultiTurnConversation chat = new(model);
            chat.MaximumCompletionTokens = 2048;
            chat.SamplingMode = new RandomSampling { Temperature = 0.8f };
            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            // Select and connect to a local MCP server via stdio
            McpClient mcpClient = SelectStdioMcpServer(chat);

            ShowSpecialPrompts();

            const string FIRST_MESSAGE = "Greet the user, then briefly enumerate the available tools; do not describe JSON schemas or parameters.";
            string mode = "chat";
            string prompt = FIRST_MESSAGE;

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                TextGenerationResult result;
                CancellationTokenSource cts = new(TimeSpan.FromMinutes(2));

                if (mode == "regenerate")
                {
                    result = chat.RegenerateResponse(cts.Token);
                    mode = "chat";
                }
                else if (mode == "continue")
                {
                    result = chat.ContinueLastAssistantResponse(cts.Token);
                    mode = "chat";
                }
                else
                {
                    result = chat.Submit(prompt, cts.Token);
                }

                Console.Write("\n(gen. tokens: {0} - stop reason: {1} - quality score: {2} - speed: {3} tok/s - ctx usage: {4}/{5})",
                    result.GeneratedTokens.Count,
                    result.TerminationReason,
                    Math.Round(result.QualityScore, 2),
                    Math.Round(result.TokenGenerationRate, 2),
                    result.ContextTokens.Count,
                    result.ContextSize);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

                if (string.Compare(prompt, "/reset", true) == 0)
                {
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
                else if (string.Compare(prompt, "/regenerate", true) == 0)
                {
                    mode = "regenerate";
                }
                else if (string.Compare(prompt, "/continue", true) == 0)
                {
                    mode = "continue";
                }
                else if (string.Compare(prompt, "/server", true) == 0)
                {
                    if (mcpClient != null)
                    {
                        chat.Tools.Remove(mcpClient.Tools);
                        mcpClient.Dispose();
                    }

                    // Select a new MCP server
                    mcpClient = SelectStdioMcpServer(chat);

                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
            }

            // Clean up
            mcpClient?.Dispose();

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            Console.ReadKey();
        }

        private static McpClient SelectStdioMcpServer(MultiTurnConversation chat)
        {
            Console.Clear();
            Console.WriteLine("=== Stdio MCP Server Selection ===\n");
            Console.WriteLine("Select a local MCP server to connect via stdio transport:\n");

            Console.WriteLine("--- Node.js MCP Servers (requires Node.js/npx) ---");
            Console.WriteLine("0 - Filesystem Server - Read/write files in a directory");
            Console.WriteLine("1 - Memory Server - In-memory key-value storage");
            Console.WriteLine("2 - Fetch Server - Make HTTP requests");
            Console.WriteLine();
            Console.WriteLine("--- Python MCP Servers (requires Python/uvx) ---");
            Console.WriteLine("3 - Git Server - Git repository operations");
            Console.WriteLine("4 - SQLite Server - SQLite database operations");
            Console.WriteLine();
            Console.WriteLine("--- Custom Server ---");
            Console.WriteLine("5 - Custom command (specify your own server)");
            Console.WriteLine();
            Console.Write("> ");

            string? serverInput = Console.ReadLine();
            McpClient mcpClient;

            switch (serverInput?.Trim())
            {
                case "0":
                    mcpClient = CreateFilesystemServer();
                    break;

                case "1":
                    mcpClient = CreateMemoryServer();
                    break;

                case "2":
                    mcpClient = CreateFetchServer();
                    break;

                case "3":
                    mcpClient = CreateGitServer();
                    break;

                case "4":
                    mcpClient = CreateSqliteServer();
                    break;

                case "5":
                    mcpClient = CreateCustomServer();
                    break;

                default:
                    Console.WriteLine("Invalid selection. Using filesystem server with current directory.");
                    mcpClient = CreateFilesystemServer();
                    break;
            }

            // Set up event handlers
            SetupEventHandlers(mcpClient);

            // Register tools with chat
            chat.Tools.Register(mcpClient);

            return mcpClient;
        }

        private static McpClient CreateFilesystemServer()
        {
            Console.Write("\nEnter the directory path to allow access to (or press Enter for current directory): ");
            string? dirPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(dirPath))
            {
                dirPath = Environment.CurrentDirectory;
            }

            Console.WriteLine($"\nStarting filesystem MCP server for: {dirPath}");
            Console.WriteLine("Command: npx @modelcontextprotocol/server-filesystem {0}\n", dirPath);

            var options = new StdioTransportOptions
            {
                Command = "npx",
                Arguments = $"@modelcontextprotocol/server-filesystem \"{dirPath}\"",
                StderrHandler = line => LogServerStderr(line),
                RequestTimeout = TimeSpan.FromSeconds(60),
                GracefulShutdown = true
            };

            return McpClient.ForStdio(options);
        }

        private static McpClient CreateMemoryServer()
        {
            Console.WriteLine("\nStarting memory MCP server...");
            Console.WriteLine("Command: npx @modelcontextprotocol/server-memory\n");

            return McpClientBuilder.ForStdio("npx", "@modelcontextprotocol/server-memory")
                .WithStderrHandler(line => LogServerStderr(line))
                .WithRequestTimeout(TimeSpan.FromSeconds(60))
                .Build();
        }

        private static McpClient CreateFetchServer()
        {
            Console.WriteLine("\nStarting fetch MCP server...");
            Console.WriteLine("Command: npx @modelcontextprotocol/server-fetch\n");

            return McpClientBuilder.ForStdio("npx", "@modelcontextprotocol/server-fetch")
                .WithStderrHandler(line => LogServerStderr(line))
                .WithRequestTimeout(TimeSpan.FromSeconds(120))
                .Build();
        }

        private static McpClient CreateGitServer()
        {
            Console.Write("\nEnter the repository path (or press Enter for current directory): ");
            string? repoPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(repoPath))
            {
                repoPath = Environment.CurrentDirectory;
            }

            Console.WriteLine($"\nStarting Git MCP server for: {repoPath}");
            Console.WriteLine("Command: uvx mcp-server-git --repository {0}\n", repoPath);

            return McpClientBuilder.ForStdio("uvx", $"mcp-server-git --repository \"{repoPath}\"")
                .WithStderrHandler(line => LogServerStderr(line))
                .WithRequestTimeout(TimeSpan.FromSeconds(60))
                .Build();
        }

        private static McpClient CreateSqliteServer()
        {
            Console.Write("\nEnter the SQLite database path: ");
            string? dbPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(dbPath))
            {
                dbPath = "database.db";
            }

            Console.WriteLine($"\nStarting SQLite MCP server for: {dbPath}");
            Console.WriteLine("Command: uvx mcp-server-sqlite --db-path {0}\n", dbPath);

            return McpClientBuilder.ForStdio("uvx", $"mcp-server-sqlite --db-path \"{dbPath}\"")
                .WithStderrHandler(line => LogServerStderr(line))
                .WithRequestTimeout(TimeSpan.FromSeconds(60))
                .Build();
        }

        private static McpClient CreateCustomServer()
        {
            Console.Write("\nEnter the command to run (e.g., 'python', 'node', 'npx'): ");
            string? command = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(command))
            {
                throw new ArgumentException("Command is required.");
            }

            Console.Write("Enter the arguments (e.g., '-m myserver --verbose'): ");
            string? arguments = Console.ReadLine();

            Console.Write("Enter working directory (or press Enter for current): ");
            string? workingDir = Console.ReadLine();

            Console.WriteLine($"\nStarting custom MCP server...");
            Console.WriteLine("Command: {0} {1}\n", command.Trim(), arguments ?? "");

            var builder = McpClientBuilder.ForStdio(command.Trim(), arguments)
                .WithStderrHandler(line => LogServerStderr(line))
                .WithRequestTimeout(TimeSpan.FromSeconds(60));

            if (!string.IsNullOrWhiteSpace(workingDir))
            {
                builder.WithWorkingDirectory(workingDir.Trim());
            }

            return builder.Build();
        }

        private static void SetupEventHandlers(McpClient mcpClient)
        {
            // Handle catalog changes
            mcpClient.CatalogChanged += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[MCP] Catalog changed - Kind: {0}, Method: {1}, Time: {2}",
                    e.Kind,
                    e.Method,
                    e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"));
                Console.ResetColor();
            };

            // Handle transport disconnection (stdio-specific)
            if (mcpClient.Transport is StdioTransport stdioTransport)
            {
                stdioTransport.Disconnected += (sender, e) =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[MCP] Transport disconnected: {0}", e.Reason);
                    if (e.Exception != null)
                    {
                        Console.WriteLine("[MCP] Exception: {0}", e.Exception.Message);
                    }
                    Console.ResetColor();
                };
            }

            // Log received messages
            mcpClient.Received += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var body = string.IsNullOrWhiteSpace(e.BodySnippet) ? "empty" : TruncateMessage(e.BodySnippet);
                Console.WriteLine("\n[MCP Received] Method: {0}, Status: {1}, Body: {2}", e.Method, e.StatusCode, body);
                Console.ResetColor();
            };

            // Log sent messages
            mcpClient.Sending += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n[MCP Sending] {0} | {1}",
                    e.Method,
                    TruncateMessage(e.Parameters == null ? "null" : JsonSerializer.Serialize(e.Parameters)));
                Console.ResetColor();
            };
        }

        private static void LogServerStderr(string line)
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("[Server] {0}", line);
            Console.ResetColor();
        }

        private static string TruncateMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return string.Empty;
            }

            const int maxLength = 150;
            return (message.Length <= maxLength) ? message : message.Substring(0, maxLength) + "...";
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Commands --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/continue' to continue last assistant message.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.");
            Console.WriteLine("Use '/server' to switch to a different MCP server.\n\n");
        }

        private static void Chat_AfterTextCompletion(
            object? sender,
            LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue; break;
                case LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.Red; break;
                case LMKit.TextGeneration.Chat.TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White; break;
            }
            Console.Write(e.Text);
        }
    }
}
