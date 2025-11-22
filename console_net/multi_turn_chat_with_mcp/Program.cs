using LMKit.Mcp.Client;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.Json;

namespace multi_turn_chat_with_mcp
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk?download=true";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GRANITE_4_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/granite-4.0-h-tiny-gguf/resolve/main/Granite-4.0-H-Tiny-64x994M-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf?download=true";

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
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = UTF8Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B Medium (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Microsoft Phi-4 14.7B Mini (requires approximately 11 GB of VRAM)");
            Console.WriteLine("6 - IBM Granite 4 7B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("7 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0": modelLink = DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH; break;
                case "1": modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH; break;
                case "2": modelLink = DEFAULT_GEMMA3_4B_MODEL_PATH; break;
                case "3": modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH; break;
                case "4": modelLink = DEFAULT_QWEN3_8B_MODEL_PATH; break;
                case "5": modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH; break;
                case "6": modelLink = DEFAULT_GRANITE_4_7B_MODEL_PATH; break;
                case "7": modelLink = DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH; break;
                default: modelLink = input.Trim().Trim('"'); break;
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
                prompt = Console.ReadLine();

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
                    mcpClient = SelectMcpServer(chat);

                    //
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
            }

            // Clean up
            mcpClient?.Dispose();

            Console.WriteLine("The chat ended. Press any key to exit the application.");
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
            Console.WriteLine("6 - Peek – Experiences/Activities");
            Console.WriteLine("7 - Currency – FX conversion");
            Console.WriteLine("8 - Find-A-Domain - Domain search & WHOIS lookup");
            Console.WriteLine();
            Console.WriteLine("=== With Authentication ===");
            Console.WriteLine("9 - Hugging Face MCP - Models, datasets, spaces");
            Console.WriteLine();
            Console.Write("Other entry: A custom MCP server URI\n\n> ");

            string serverInput = Console.ReadLine();
            string serverUri;
            bool requiresAuth = false;

            switch (serverInput.Trim())
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
                    serverUri = serverInput.Trim().Trim('"');
                    Console.Write("Does this server require authentication? (y/n): ");
                    string authResponse = Console.ReadLine();
                    requiresAuth = authResponse != null && authResponse.Trim().ToLower() == "y";
                    break;
            }

            Console.Clear();
            Console.WriteLine("Connecting to MCP server: {0}\n", serverUri);

            McpClient mcpClient = new(serverUri);

            // Handle catalog changes (tools, resources, prompts updates)
            mcpClient.CatalogChanged += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n[MCP] Catalog changed - Kind: {0}, Method: {1}, Time: {2}",
                    e.Kind,
                    e.Method,
                    e.ReceivedAt.ToLocalTime().ToString("HH:mm:ss"));

                if (!string.IsNullOrEmpty(e.RawNotification))
                {
                    Console.WriteLine("[MCP] Notification: {0}", TruncateMessage(e.RawNotification));
                }

                Console.ResetColor();
            };

            // Handle authentication failures (event) - provides EventArgs with StatusCode
            mcpClient.AuthFailed += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[MCP] Authentication failed! Status code: {0}. Please check your token.", e.StatusCode);
                Console.ResetColor();
            };

            // Log received messages from MCP server (event) - provides EventArgs with Method, StatusCode, BodySnippet
            mcpClient.Received += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                var body = string.IsNullOrWhiteSpace(e.BodySnippet) ? "empty" : TruncateMessage(e.BodySnippet);
                Console.WriteLine("\n[MCP Received] Method: {0}, Status: {1}, Body: {2}", e.Method, e.StatusCode, body);
                Console.ResetColor();
            };

            // Log sent messages to MCP server (event) - provides EventArgs with Method, Parameters
            mcpClient.Sending += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\n[MCP Sending] {0} | {1}",
                    e.Method,
                    TruncateMessage(e.Parameters == null ? "null" : JsonSerializer.Serialize(e.Parameters)));
                Console.ResetColor();
            };

            if (requiresAuth)
            {
                Console.Write("Enter your authentication token (or press Enter to skip): ");
                string token = Console.ReadLine();
                if (!string.IsNullOrWhiteSpace(token))
                {
                    mcpClient.SetBearerToken(token.Trim());
                    Console.WriteLine("Token configured successfully\n");
                }
            }

            chat.Tools.Register(mcpClient);

            return mcpClient;
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
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/continue' to continue last assistant message.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.");
            Console.WriteLine("Use '/server' to switch to a different MCP server.\n\n");
        }

        private static void Chat_AfterTextCompletion(
            object sender,
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