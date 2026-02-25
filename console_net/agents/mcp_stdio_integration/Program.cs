using LMKit.Mcp.Client;
using LMKit.Mcp.Transport;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.Json;

namespace mcp_stdio_integration
{
    internal class Program
    {
        static bool _isDownloading;

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write("\rDownloading model {0:0.00}%", Math.Round((double)bytesRead / contentLength.Value * 100, 2));
            else
                Console.Write("\rDownloading model {0} bytes", bytesRead);
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write("\rLoading model {0}%", Math.Round(progress * 100));
            return true;
        }

        static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3:8b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== MCP Stdio Transport Demo ===\n");
            Console.WriteLine("This demo shows how to connect to local MCP servers using stdio transport.");
            Console.WriteLine("The stdio transport spawns a subprocess and communicates via stdin/stdout.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM)");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen-3.5 27B     (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "");

            Console.Clear();

            MultiTurnConversation chat = new(model);
            chat.MaximumCompletionTokens = 2048;
            chat.SamplingMode = new RandomSampling { Temperature = 0.8f };
            chat.AfterTextCompletion += OnAfterTextCompletion;

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

                    mcpClient = SelectStdioMcpServer(chat);
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
            }

            mcpClient?.Dispose();

            Console.WriteLine("Demo ended. Press any key to exit.");
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
                case "0": mcpClient = CreateFilesystemServer(); break;
                case "1": mcpClient = CreateMemoryServer(); break;
                case "2": mcpClient = CreateFetchServer(); break;
                case "3": mcpClient = CreateGitServer(); break;
                case "4": mcpClient = CreateSqliteServer(); break;
                case "5": mcpClient = CreateCustomServer(); break;
                default:
                    Console.WriteLine("Invalid selection. Using filesystem server with current directory.");
                    mcpClient = CreateFilesystemServer();
                    break;
            }

            SetupEventHandlers(mcpClient);
            chat.Tools.Register(mcpClient);

            return mcpClient;
        }

        private static McpClient CreateFilesystemServer()
        {
            Console.Write("\nEnter the directory path to allow access to (or press Enter for current directory): ");
            string? dirPath = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(dirPath))
                dirPath = Environment.CurrentDirectory;

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
                repoPath = Environment.CurrentDirectory;

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
                dbPath = "database.db";

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
                throw new ArgumentException("Command is required.");

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
                builder.WithWorkingDirectory(workingDir.Trim());

            return builder.Build();
        }

        private static void SetupEventHandlers(McpClient mcpClient)
        {
            mcpClient.CatalogChanged += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[MCP] Catalog changed - Kind: {0}, Method: {1}, Time: {2}",
                    e.Kind,
                    e.Method,
                    e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"));
                Console.ResetColor();
            };

            if (mcpClient.Transport is StdioTransport stdioTransport)
            {
                stdioTransport.Disconnected += (sender, e) =>
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\n[MCP] Transport disconnected: {0}", e.Reason);
                    if (e.Exception != null)
                        Console.WriteLine("[MCP] Exception: {0}", e.Exception.Message);
                    Console.ResetColor();
                };
            }

            mcpClient.Received += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var body = string.IsNullOrWhiteSpace(e.BodySnippet) ? "empty" : TruncateMessage(e.BodySnippet);
                Console.WriteLine("\n[MCP Received] Method: {0}, Status: {1}, Body: {2}", e.Method, e.StatusCode, body);
                Console.ResetColor();
            };

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
                return string.Empty;

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

        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
        }
    }
}
