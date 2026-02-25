using LMKit.Mcp.Abstractions;
using LMKit.Mcp.Client;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.Json;

namespace mcp_integration
{
    internal class Program
    {
        // Public servers (No Authentication)
        static readonly string ECHO_MCP_URI = "https://echo.mcp.inevitable.fyi/mcp";
        static readonly string TIME_MCP_URI = "https://time.mcp.inevitable.fyi/mcp";
        static readonly string DEEPWIKI_MCP_URI = "https://mcp.deepwiki.com/mcp";
        static readonly string TEXT_EXTRACTOR_MCP_URI = "https://text-extractor.mcp.inevitable.fyi/mcp";
        static readonly string EVERYTHING_MCP_URI = "https://everything.mcp.inevitable.fyi/mcp";
        static readonly string MS_LEARN_DOCS_MCP_URI = "https://learn.microsoft.com/api/mcp";
        static readonly string PEEK_MCP_URI = "https://mcp.peek.com/mcp";
        static readonly string WESBOS_CURRENCY_MCP_URI = "https://currency-mcp.wesbos.com/mcp";
        static readonly string FIND_A_DOMAIN_MCP_URI = "https://api.findadomain.dev/mcp";

        // Requires Authentication
        static readonly string HUGGINGFACE_MCP_URI = "https://huggingface.co/mcp";

        static bool _isDownloading;
        static LM? _model;

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
            _model = LoadModel(input ?? "");

            Console.Clear();

            MultiTurnConversation chat = new(_model);
            chat.MaximumCompletionTokens = 2048;
            chat.SamplingMode = new RandomSampling { Temperature = 0.8f };
            chat.AfterTextCompletion += OnAfterTextCompletion;

            // Initial MCP server selection
            McpClient mcpClient = SelectMcpServer(chat);

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

                    mcpClient = SelectMcpServer(chat);
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
                else if (string.Compare(prompt, "/resources", true) == 0)
                {
                    ShowResources(mcpClient);
                    prompt = " ";
                    continue;
                }
                else if (string.Compare(prompt, "/capabilities", true) == 0)
                {
                    ShowCapabilities(mcpClient);
                    prompt = " ";
                    continue;
                }
                else if (string.Compare(prompt, "/roots", true) == 0)
                {
                    ManageRoots(mcpClient);
                    prompt = " ";
                    continue;
                }
                else if (string.Compare(prompt, "/loglevel", true) == 0)
                {
                    SetLogLevel(mcpClient);
                    prompt = " ";
                    continue;
                }
            }

            mcpClient?.Dispose();

            Console.WriteLine("Demo ended. Press any key to exit.");
            Console.ReadKey();
        }

        private static McpClient SelectMcpServer(MultiTurnConversation chat)
        {
            Console.Clear();
            Console.WriteLine("Please select the MCP server you want to use:\n");

            Console.WriteLine("=== Without Authentication ===");
            Console.WriteLine("0 - Echo MCP - Echo tool for client/debug sanity checks");
            Console.WriteLine("1 - Time MCP - Time & timezone utilities");
            Console.WriteLine("2 - DeepWiki - Ask questions over public GitHub repos");
            Console.WriteLine("3 - Text Extractor - Extract clean text/Markdown from URLs");
            Console.WriteLine("4 - Everything - Reference server (prompts/resources/tools)");
            Console.WriteLine("5 - Microsoft Learn Docs - Search & fetch MS Learn docs");
            Console.WriteLine("6 - Peek - Experiences/Activities");
            Console.WriteLine("7 - Currency - FX conversion");
            Console.WriteLine("8 - Find-A-Domain - Domain search & WHOIS lookup");
            Console.WriteLine();
            Console.WriteLine("=== With Authentication ===");
            Console.WriteLine("9 - Hugging Face MCP - Models, datasets, spaces");
            Console.WriteLine();
            Console.Write("Other entry: A custom MCP server URI\n\n> ");

            string? serverInput = Console.ReadLine();
            string serverUri;
            bool requiresAuth = false;

            switch (serverInput?.Trim())
            {
                case "0": serverUri = ECHO_MCP_URI; break;
                case "1": serverUri = TIME_MCP_URI; break;
                case "2": serverUri = DEEPWIKI_MCP_URI; break;
                case "3": serverUri = TEXT_EXTRACTOR_MCP_URI; break;
                case "4": serverUri = EVERYTHING_MCP_URI; break;
                case "5": serverUri = MS_LEARN_DOCS_MCP_URI; break;
                case "6": serverUri = PEEK_MCP_URI; break;
                case "7": serverUri = WESBOS_CURRENCY_MCP_URI; break;
                case "8": serverUri = FIND_A_DOMAIN_MCP_URI; break;
                case "9": serverUri = HUGGINGFACE_MCP_URI; requiresAuth = true; break;
                default:
                    serverUri = serverInput!.Trim().Trim('"');
                    Console.Write("Does this server require authentication? (y/n): ");
                    string? authResponse = Console.ReadLine();
                    requiresAuth = authResponse != null && authResponse.Trim().ToLower() == "y";
                    break;
            }

            Console.Clear();
            Console.WriteLine("Connecting to MCP server: {0}\n", serverUri);

            McpClient mcpClient = new(serverUri);

            mcpClient.CatalogChanged += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[MCP] Catalog changed - Kind: {0}, Method: {1}, Time: {2}",
                    e.Kind,
                    e.Method,
                    e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"));

                if (!string.IsNullOrEmpty(e.RawNotification))
                    Console.WriteLine("[MCP] Notification: {0}", TruncateMessage(e.RawNotification));

                Console.ResetColor();
            };

            mcpClient.AuthFailed += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[MCP] Authentication failed! Status code: {0}. Please check your token.", e.StatusCode);
                Console.ResetColor();
            };

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

            mcpClient.SetSamplingHandler((request, cancellationToken) =>
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine("\n[MCP Sampling] Server requested completion ({0} message(s), max {1} tokens)",
                    request.Messages.Count,
                    request.MaxTokens);

                if (!string.IsNullOrEmpty(request.SystemPrompt))
                    Console.WriteLine("[MCP Sampling] System prompt: {0}", TruncateMessage(request.SystemPrompt));

                Console.ResetColor();

                if (_model != null)
                {
                    var samplingChat = new MultiTurnConversation(_model);
                    samplingChat.MaximumCompletionTokens = request.MaxTokens > 0 ? request.MaxTokens : 512;

                    if (!string.IsNullOrEmpty(request.SystemPrompt))
                        samplingChat.SystemPrompt = request.SystemPrompt;

                    string samplingPrompt = "Hello";
                    foreach (var msg in request.Messages)
                    {
                        if (msg.Role == McpMessageRole.User && msg.Content?.Text != null)
                            samplingPrompt = msg.Content.Text;
                    }

                    var samplingResult = samplingChat.Submit(samplingPrompt, cancellationToken);

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine("[MCP Sampling] Completed ({0} tokens)", samplingResult.GeneratedTokens.Count);
                    Console.ResetColor();

                    return Task.FromResult(McpSamplingResponse.FromText(
                        samplingResult.Completion,
                        "local-model",
                        McpStopReason.EndTurn));
                }

                return Task.FromResult(McpSamplingResponse.FromText(
                    "Model not available for sampling.",
                    "none",
                    McpStopReason.EndTurn));
            });

            mcpClient.SetElicitationHandler((request, cancellationToken) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n[MCP Elicitation] Server requests input: {0}", request.Message);

                if (!string.IsNullOrEmpty(request.RequestedSchema))
                    Console.WriteLine("[MCP Elicitation] Schema: {0}", TruncateMessage(request.RequestedSchema));

                Console.Write("[MCP Elicitation] Your response (or 'decline'): ");
                Console.ResetColor();

                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput) ||
                    string.Compare(userInput.Trim(), "decline", true) == 0)
                {
                    return Task.FromResult(McpElicitationResponse.Decline());
                }

                var content = new Dictionary<string, object> { ["response"] = userInput.Trim() };
                return Task.FromResult(McpElicitationResponse.Accept(content));
            });

            mcpClient.RootsRequested += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine("\n[MCP Roots] Server requested root list ({0} root(s) configured)",
                    mcpClient.Roots.Count);
                Console.ResetColor();
            };

            mcpClient.ProgressReceived += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                string progressText = e.Percentage.HasValue
                    ? $"{e.Percentage.Value:F0}%"
                    : $"{e.Progress}/{e.Total?.ToString() ?? "?"}";
                Console.Write("\r[MCP Progress] {0}", progressText);
                if (!string.IsNullOrEmpty(e.Message))
                    Console.Write(" - {0}", e.Message);
                Console.WriteLine();
                Console.ResetColor();
            };

            mcpClient.LogMessageReceived += (sender, e) =>
            {
                ConsoleColor color = e.Level switch
                {
                    McpLogLevel.Error or McpLogLevel.Critical or
                    McpLogLevel.Alert or McpLogLevel.Emergency => ConsoleColor.Red,
                    McpLogLevel.Warning => ConsoleColor.DarkYellow,
                    McpLogLevel.Notice or McpLogLevel.Info => ConsoleColor.DarkGray,
                    _ => ConsoleColor.DarkGray
                };
                Console.ForegroundColor = color;
                Console.WriteLine("[MCP Log] [{0}] {1}: {2}",
                    e.Level,
                    e.Logger ?? "server",
                    TruncateMessage(e.Data ?? ""));
                Console.ResetColor();
            };

            mcpClient.ResourceUpdated += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[MCP Resource] Updated: {0} at {1}",
                    e.Uri,
                    e.Timestamp.ToLocalTime().ToString("HH:mm:ss"));
                Console.ResetColor();
            };

            mcpClient.CancellationReceived += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[MCP Cancel] Request {0} cancelled{1}",
                    e.RequestId,
                    string.IsNullOrEmpty(e.Reason) ? "" : $": {e.Reason}");
                Console.ResetColor();
            };

            if (requiresAuth)
            {
                Console.Write("Enter your authentication token (or press Enter to skip): ");
                string? token = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    mcpClient.SetBearerToken(token.Trim());
                    Console.WriteLine("Token configured successfully\n");
                }
            }

            mcpClient.AddRoot(Environment.CurrentDirectory, "working-directory", notifyServer: false);

            chat.Tools.Register(mcpClient);

            ShowCapabilities(mcpClient);

            return mcpClient;
        }

        private static void ShowCapabilities(McpClient mcpClient)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n-- Server Capabilities --");

            if (mcpClient.HasCapability(McpServerCapabilities.Tools))
                Console.WriteLine("  [x] Tools");
            if (mcpClient.HasCapability(McpServerCapabilities.Resources))
                Console.WriteLine("  [x] Resources");
            if (mcpClient.HasCapability(McpServerCapabilities.Prompts))
                Console.WriteLine("  [x] Prompts");
            if (mcpClient.HasCapability(McpServerCapabilities.Logging))
                Console.WriteLine("  [x] Logging");
            if (mcpClient.HasCapability(McpServerCapabilities.Completions))
                Console.WriteLine("  [x] Completions");

            Console.WriteLine("  Session: {0}", mcpClient.SessionId ?? "n/a");
            Console.WriteLine("  Roots: {0} configured", mcpClient.Roots.Count);
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void ShowResources(McpClient mcpClient)
        {
            if (!mcpClient.HasCapability(McpServerCapabilities.Resources))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("This server does not support resources.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n-- Resources --");

            var resources = mcpClient.GetResources();
            if (resources.Count == 0)
            {
                Console.WriteLine("  No resources available.");
            }
            else
            {
                int i = 0;
                foreach (var res in resources)
                {
                    Console.WriteLine("  {0}. {1} ({2})", i++, res.Name, res.Uri);
                    if (!string.IsNullOrEmpty(res.Description))
                        Console.WriteLine("     {0}", TruncateMessage(res.Description));
                }
            }

            var templates = mcpClient.GetResourceTemplates();
            if (templates.Count > 0)
            {
                Console.WriteLine("\n-- Resource Templates --");
                foreach (var tmpl in templates)
                {
                    Console.WriteLine("  {0}: {1}", tmpl.Name, tmpl.UriTemplate);
                    if (!string.IsNullOrEmpty(tmpl.Description))
                        Console.WriteLine("     {0}", TruncateMessage(tmpl.Description));
                }
            }

            Console.Write("\nEnter a resource URI to subscribe (or press Enter to skip): ");
            Console.ResetColor();
            string? subUri = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(subUri))
            {
                mcpClient.SubscribeToResource(subUri.Trim());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Subscribed to: {0}", subUri.Trim());
                Console.ResetColor();
            }
        }

        private static void ManageRoots(McpClient mcpClient)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n-- Filesystem Roots --");

            if (mcpClient.Roots.Count == 0)
            {
                Console.WriteLine("  No roots configured.");
            }
            else
            {
                for (int i = 0; i < mcpClient.Roots.Count; i++)
                    Console.WriteLine("  {0}. {1} ({2})", i, mcpClient.Roots[i].Name, mcpClient.Roots[i].Uri);
            }

            Console.Write("\nEnter a directory path to add as root (or press Enter to skip): ");
            Console.ResetColor();
            string? rootPath = Console.ReadLine();
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                Console.Write("Root name (optional): ");
                string? rootName = Console.ReadLine();
                mcpClient.AddRoot(rootPath.Trim(), string.IsNullOrWhiteSpace(rootName) ? null : rootName.Trim());
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Root added: {0}", rootPath.Trim());
                Console.ResetColor();
            }
        }

        private static void SetLogLevel(McpClient mcpClient)
        {
            if (!mcpClient.HasCapability(McpServerCapabilities.Logging))
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine("This server does not support logging.");
                Console.ResetColor();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n-- Set Server Log Level --");
            Console.WriteLine("  0 - Debug");
            Console.WriteLine("  1 - Info");
            Console.WriteLine("  2 - Notice");
            Console.WriteLine("  3 - Warning");
            Console.WriteLine("  4 - Error");
            Console.Write("> ");
            Console.ResetColor();

            string? levelInput = Console.ReadLine();
            McpLogLevel level = levelInput?.Trim() switch
            {
                "0" => McpLogLevel.Debug,
                "1" => McpLogLevel.Info,
                "2" => McpLogLevel.Notice,
                "3" => McpLogLevel.Warning,
                "4" => McpLogLevel.Error,
                _ => McpLogLevel.Info
            };

            mcpClient.SetLogLevel(level);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Log level set to: {0}", level);
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
            Console.WriteLine("Use '/server' to switch to a different MCP server.");
            Console.WriteLine("Use '/resources' to browse resources and subscribe to updates.");
            Console.WriteLine("Use '/capabilities' to view server capabilities.");
            Console.WriteLine("Use '/roots' to manage filesystem roots.");
            Console.WriteLine("Use '/loglevel' to set the server log level.\n\n");
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
