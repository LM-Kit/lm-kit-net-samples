using LMKit.Agents;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using research_assistant.Tools;
using System.Text;

namespace research_assistant
{
    internal class Program
    {
        static readonly string DEFAULT_GEMMA3_12B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_14B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-14b-instruct-gguf/resolve/main/Qwen3-14B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI4_14B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
        static readonly string DEFAULT_GLM_4_7_FLASH_MODEL_PATH = @"https://huggingface.co/lm-kit/glm-4.7-flash-gguf/resolve/main/GLM-4.7-Flash-64x2.6B-Q4_K_M.gguf";
        static readonly string DEFAULT_MISTRAL_SMALL_24B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-small-3.2-2506-24b-instruct-gguf/resolve/main/Mistral-Small-3.2-24B-Instruct-2506-Q4_K_M.gguf";
        static readonly string DEFAULT_MAGISTRAL_SMALL_24B_MODEL_PATH = @"https://huggingface.co/lm-kit/magistral-small-2509-24b-gguf/resolve/main/Magistral-Small-2509-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_27B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-27b-instruct-lmk/resolve/main/gemma-3-27b-it-Q4_K_M.lmk";

        static bool _isDownloading;

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
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Research Assistant Agent Demo ===\n");
            Console.WriteLine("This demo showcases the ReAct (Reasoning + Acting) planning strategy.");
            Console.WriteLine("Watch the agent think, search, take notes, and synthesize findings.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B (requires approximately 10 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Mistral Small 3.2 24B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("6 - Mistral Magistral Small 1.2 24B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("7 - Google Gemma 3 27B (requires approximately 18 GB of VRAM)");
            Console.WriteLine("8 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink = input?.Trim() switch
            {
                "0" => DEFAULT_QWEN3_8B_MODEL_PATH,
                "1" => DEFAULT_GEMMA3_12B_MODEL_PATH,
                "2" => DEFAULT_QWEN3_14B_MODEL_PATH,
                "3" => DEFAULT_PHI4_14B_MODEL_PATH,
                "4" => DEFAULT_GPT_OSS_20B_MODEL_PATH,
                "5" => DEFAULT_MISTRAL_SMALL_24B_MODEL_PATH,
                "6" => DEFAULT_MAGISTRAL_SMALL_24B_MODEL_PATH,
                "7" => DEFAULT_GEMMA3_27B_MODEL_PATH,
                "8" => DEFAULT_GLM_4_7_FLASH_MODEL_PATH,
                _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : DEFAULT_QWEN3_8B_MODEL_PATH
            };

            // Load model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.WriteLine("=== Research Assistant Agent ===\n");
            Console.WriteLine("Enter a research topic or question. The agent will:");
            Console.WriteLine("  1. Think about what information is needed");
            Console.WriteLine("  2. Search for relevant information");
            Console.WriteLine("  3. Take notes on important findings");
            Console.WriteLine("  4. Synthesize a comprehensive answer\n");
            Console.WriteLine("Type 'quit' to exit.\n");

            // Create tools
            // Use the built-in WebSearchTool with DuckDuckGo (no API key required)
            // Other providers available: Brave, Tavily, Serper, SearXNG
            // Example with Brave: BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Brave, "your-api-key")
            // Example with Tavily: BuiltInTools.CreateWebSearch(WebSearchTool.Provider.Tavily, Environment.GetEnvironmentVariable("TAVILY_API_KEY"))
            var webSearchTool = BuiltInTools.WebSearch;
            var noteTakingTool = new NoteTakingTool();
            var getNotesTool = new GetNotesTool(noteTakingTool);

            // Create agent with ReAct planning strategy
            var agent = Agent.CreateBuilder(model)
                .WithPersona(@"You are an expert research analyst. Your job is to thoroughly research topics
by searching for information, taking organized notes, and synthesizing findings into clear,
well-structured summaries. Always cite your sources and distinguish between facts and opinions.
Be thorough but concise in your final summaries.")
                .WithPlanning(PlanningStrategy.ReAct)
                .WithTools(tools =>
                {
                    tools.Register(webSearchTool);
                    tools.Register(noteTakingTool);
                    tools.Register(getNotesTool);
                })
                .WithMaxIterations(10)
                .Build();

            // Create executor and attach event handler for streaming output
            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nResearch Topic: ");
                Console.ResetColor();

                string? topic = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(topic) || topic.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                // Clear notes for new research session
                noteTakingTool.ClearNotes();

                Console.WriteLine("\n--- Agent is researching... ---\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                    // Execute agent
                    var result = executor.Execute(
                        agent,
                        $"Research the following topic and provide a comprehensive summary with key findings: {topic}",
                        cts.Token);

                    Console.WriteLine("\n\n--- Research Complete ---\n");

                    // Display collected notes
                    var notes = noteTakingTool.GetAllNotes();
                    if (notes.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Notes collected during research ({notes.Count} total):");
                        Console.ResetColor();

                        foreach (var note in notes)
                        {
                            Console.WriteLine($"  [{note.Category}] {note.Content}");
                            if (!string.IsNullOrEmpty(note.Source))
                            {
                                Console.WriteLine($"           Source: {note.Source}");
                            }
                        }
                    }

                    // Display stats
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"\n(Inferences: {result.InferenceCount} | Status: {result.Status})");
                    Console.ResetColor();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nResearch timed out.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\nThank you for using Research Assistant. Press any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Event handler to display agent output in real-time with color-coded segments.
        /// </summary>
        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            switch (e.SegmentType)
            {
                case TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    break;
                case TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write(e.Text);
        }
    }
}
