using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace web_search_assistant
{
    internal class Program
    {
        // ── Model catalog ──────────────────────────────────────────────
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_12B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
        static readonly string DEFAULT_GLM_4_7_FLASH_MODEL_PATH = @"https://huggingface.co/lm-kit/glm-4.7-flash-gguf/resolve/main/GLM-4.7-Flash-64x2.6B-Q4_K_M.gguf";

        static bool _isDownloading;

        // Tracks whether we are currently inside a tool invocation segment,
        // so we can print clear boundaries around tool calls.
        static bool _insideToolCall;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
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
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        private static void Main(string[] args)
        {
            // Set your license key here if you have one.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            // ── Banner ─────────────────────────────────────────────────
            Console.Clear();
            PrintBanner();
            Console.WriteLine("A general-purpose assistant that autonomously searches the web");
            Console.WriteLine("when it needs fresh or factual information to answer your question.\n");
            Console.WriteLine("This demo demonstrates:\n");
            Console.WriteLine("  1. How to register the built-in WebSearchTool (DuckDuckGo, no API key)");
            Console.WriteLine("  2. How the LLM decides BY ITSELF when to search the web");
            Console.WriteLine("  3. Full call-flow visibility: reasoning, tool calls, and responses\n");

            // ── Model selection ────────────────────────────────────────
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("2 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("3 - OpenAI GPT OSS 20B (requires approximately 16 GB of VRAM) [Recommended]");
            Console.WriteLine("4 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink = input?.Trim() switch
            {
                "0" => DEFAULT_GEMMA3_12B_MODEL_PATH,
                "1" => DEFAULT_QWEN3_8B_MODEL_PATH,
                "2" => DEFAULT_PHI4_14_7B_MODEL_PATH,
                "3" => DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH,
                "4" => DEFAULT_GLM_4_7_FLASH_MODEL_PATH,
                _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : DEFAULT_QWEN3_8B_MODEL_PATH
            };

            // ── Load model ─────────────────────────────────────────────
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            PrintBanner();

            // ── Web Search provider selection ──────────────────────────
            // By default we use DuckDuckGo which requires no API key.
            // You can switch to a premium provider for better results:
            //
            //   var webSearch = BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Brave, "YOUR_BRAVE_API_KEY");
            //   var webSearch = BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Tavily, "YOUR_TAVILY_API_KEY");
            //   var webSearch = BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Serper, "YOUR_SERPER_API_KEY");
            //
            var webSearch = BuiltInTools.WebSearch;

            // ── Create the conversation with tools ─────────────────────
            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling() { Temperature = 0.7f },
                SystemPrompt = @"You are a helpful, accurate, and friendly AI assistant.

You have access to a web search tool. USE IT when:
- The user asks about current events, news, or recent information
- The user asks factual questions you are not confident about
- The user asks about specific products, prices, or availability
- The user asks about weather, sports scores, or real-time data
- The user explicitly asks you to search or look something up

DO NOT use web search when:
- The user is making casual conversation or greetings
- The question is about general knowledge you are confident about
- The user asks for creative writing, jokes, or opinions
- The user asks about programming concepts or math

When you use web search, always cite the source of the information in your answer.
When you don't use web search, just answer naturally from your knowledge."
            };

            // ── Register the built-in WebSearchTool ────────────────────
            // This is the key line: one call registers the tool, and the
            // LLM will autonomously decide when to invoke it.
            chat.Tools.Register(webSearch);

            // Also register DateTime so the model knows what "today" is.
            chat.Tools.Register(BuiltInTools.DateTimeNow);

            // ── Attach the streaming event handler ─────────────────────
            // This is where we get full visibility into the call flow.
            chat.AfterTextCompletion += OnAfterTextCompletion;

            // ── Print usage instructions ───────────────────────────────
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Color legend:                                                    ║");
            Console.WriteLine("║    White  = Assistant response (user-visible)                     ║");
            Console.WriteLine("║    Blue   = Internal reasoning (thinking)                         ║");
            Console.WriteLine("║    Red    = Tool invocation (web_search / get_datetime calls)     ║");
            Console.WriteLine("║    Green  = User / Assistant labels                               ║");
            Console.WriteLine("║    Gray   = Generation statistics                                 ║");
            Console.WriteLine("╠═══════════════════════════════════════════════════════════════════╣");
            Console.WriteLine("║  Commands:                                                        ║");
            Console.WriteLine("║    /reset       - Clear conversation history                      ║");
            Console.WriteLine("║    /regenerate  - Regenerate the last response                    ║");
            Console.WriteLine("║    /continue    - Continue the last response                      ║");
            Console.WriteLine("║    (empty)      - Exit                                            ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();

            Console.WriteLine("\nTry these example questions to see web search in action:\n");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  \"What are the latest headlines today?\"");
            Console.WriteLine("  \"Who won the last Super Bowl?\"");
            Console.WriteLine("  \"What is the current price of Bitcoin?\"");
            Console.WriteLine("  \"Tell me a joke\"  (no search needed)");
            Console.WriteLine("  \"What is 2 + 2?\"  (no search needed)");
            Console.ResetColor();

            // ── Chat loop ──────────────────────────────────────────────
            const string FIRST_MESSAGE = "Greet the user warmly. Tell them you are an assistant with web search capabilities and that you will automatically search the web when you need fresh or factual information. Give 2-3 example questions they could ask.";
            string mode = "chat";
            string prompt = FIRST_MESSAGE;

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nAssistant: ");
                Console.ResetColor();

                _insideToolCall = false;

                CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

                try
                {
                    TextGenerationResult result;

                    if (mode == "regenerate")
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[Regenerating last response...]\n");
                        Console.ResetColor();
                        result = chat.RegenerateResponse(cts.Token);
                        mode = "chat";
                    }
                    else if (mode == "continue")
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("[Continuing last response...]\n");
                        Console.ResetColor();
                        result = chat.ContinueLastAssistantResponse(cts.Token);
                        mode = "chat";
                    }
                    else
                    {
                        result = chat.Submit(prompt, cts.Token);
                    }

                    // Close any open tool-call block
                    if (_insideToolCall)
                    {
                        _insideToolCall = false;
                    }

                    // ── Print generation statistics ────────────────────
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"(tokens: {result.GeneratedTokens.Count}");
                    Console.Write($" | stop: {result.TerminationReason}");
                    Console.Write($" | quality: {Math.Round(result.QualityScore, 2)}");
                    Console.Write($" | speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s");
                    Console.Write($" | ctx: {result.ContextTokens.Count}/{result.ContextSize})");
                    Console.ResetColor();
                }
                catch (OperationCanceledException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nRequest timed out after 3 minutes.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }

                // ── Read next user input ───────────────────────────────
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

                // ── Handle special commands ─────────────────────────────
                if (string.Compare(prompt, "/reset", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    chat.ClearHistory();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("[Conversation cleared]");
                    Console.ResetColor();
                    prompt = FIRST_MESSAGE;
                }
                else if (string.Compare(prompt, "/regenerate", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    mode = "regenerate";
                }
                else if (string.Compare(prompt, "/continue", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    mode = "continue";
                }
            }

            Console.WriteLine("\nGoodbye! Press any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Streaming event handler that provides real-time, color-coded visibility
        /// into every phase of the LLM call flow: reasoning, tool calls, and responses.
        /// </summary>
        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case TextSegmentType.InternalReasoning:
                    // The model is "thinking" before answering or before calling a tool.
                    if (_insideToolCall)
                    {
                        _insideToolCall = false;
                        Console.ResetColor();
                    }
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;

                case TextSegmentType.ToolInvocation:
                    // The model is calling a tool (web_search or get_datetime).
                    // This segment contains the raw tool call JSON and its result.
                    if (!_insideToolCall)
                    {
                        _insideToolCall = true;
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("  ┌─── Tool Call ──────────────────────────────────────────");
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case TextSegmentType.UserVisible:
                    // The final response that the user sees.
                    if (_insideToolCall)
                    {
                        _insideToolCall = false;
                        Console.ForegroundColor = ConsoleColor.DarkYellow;
                        Console.WriteLine("\n  └───────────────────────────────────────────────────────");
                        Console.ResetColor();
                        Console.WriteLine();
                    }
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write(e.Text);
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║            Web Search Assistant - LM-Kit.NET Demo                 ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝\n");
            Console.ResetColor();
        }
    }
}
