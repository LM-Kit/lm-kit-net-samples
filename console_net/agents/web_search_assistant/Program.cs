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
        static bool _isDownloading;
        static bool _insideToolCall;

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
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
            PrintBanner();
            Console.WriteLine("A general-purpose assistant that autonomously searches the web");
            Console.WriteLine("when it needs fresh or factual information to answer your question.\n");
            Console.WriteLine("This demo demonstrates:\n");
            Console.WriteLine("  1. How to register the built-in WebSearchTool (DuckDuckGo, no API key)");
            Console.WriteLine("  2. How the LLM decides BY ITSELF when to search the web");
            Console.WriteLine("  3. Full call-flow visibility: reasoning, tool calls, and responses\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM)");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM) [Recommended]");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "");

            Console.Clear();
            PrintBanner();

            var webSearch = BuiltInTools.WebSearch;

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

            chat.Tools.Register(webSearch);
            chat.Tools.Register(BuiltInTools.DateTimeNow);
            chat.AfterTextCompletion += OnAfterTextCompletion;

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

                    if (_insideToolCall)
                        _insideToolCall = false;

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

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

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

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case TextSegmentType.InternalReasoning:
                    if (_insideToolCall)
                    {
                        _insideToolCall = false;
                        Console.ResetColor();
                    }
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;

                case TextSegmentType.ToolInvocation:
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
