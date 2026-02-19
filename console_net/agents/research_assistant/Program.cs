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
        static bool _isDownloading;

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
            Console.WriteLine("=== Research Assistant Agent Demo ===\n");
            Console.WriteLine("This demo showcases the ReAct (Reasoning + Acting) planning strategy.");
            Console.WriteLine("Watch the agent think, search, take notes, and synthesize findings.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "");

            Console.Clear();
            Console.WriteLine("=== Research Assistant Agent ===\n");
            Console.WriteLine("Enter a research topic or question. The agent will:");
            Console.WriteLine("  1. Think about what information is needed");
            Console.WriteLine("  2. Search for relevant information");
            Console.WriteLine("  3. Take notes on important findings");
            Console.WriteLine("  4. Synthesize a comprehensive answer\n");
            Console.WriteLine("Type 'quit' to exit.\n");

            // Create tools
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

            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nResearch Topic: ");
                Console.ResetColor();

                string? topic = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(topic) || topic.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                noteTakingTool.ClearNotes();

                Console.WriteLine("\n--- Agent is researching... ---\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                    var result = executor.Execute(
                        agent,
                        $"Research the following topic and provide a comprehensive summary with key findings: {topic}",
                        cts.Token);

                    Console.WriteLine("\n\n--- Research Complete ---\n");

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
                                Console.WriteLine($"           Source: {note.Source}");
                        }
                    }

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

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
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
