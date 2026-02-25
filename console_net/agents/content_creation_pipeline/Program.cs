using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Model;
using System.Text;

namespace content_creation_pipeline
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

        private static async Task Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Content Creation Pipeline Demo ===\n");
            Console.WriteLine("This demo showcases sequential multi-agent orchestration.");
            Console.WriteLine("Content flows through: Outliner → Writer → Editor → Fact-Checker\n");

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
            Console.WriteLine("=== Content Creation Pipeline ===\n");

            // Create the four pipeline agents
            var outlinerAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Outliner - You are an expert Content Outliner. Your job is to analyze a topic and create
a well-structured outline for an article or blog post.

Your outline should include:
- A compelling title
- An introduction section (what the article will cover)
- 3-5 main sections with clear headings
- Key points to cover under each section
- A conclusion section

Format your outline clearly with headers and bullet points.
Focus on logical flow and comprehensive coverage of the topic.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var writerAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Writer - You are a professional Content Writer. Your job is to take an outline and
expand it into engaging, well-written prose.

Guidelines:
- Write in a clear, accessible style
- Use transitions between sections
- Include relevant examples where appropriate
- Maintain a consistent tone throughout
- Aim for 400-600 words total
- Do not include meta-commentary about the writing process

Transform the outline into flowing, readable content while preserving all key points.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var editorAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Editor - You are a meticulous Editor. Your job is to refine and polish written content.

Focus on:
- Grammar and spelling corrections
- Improving sentence structure and readability
- Enhancing flow and transitions
- Removing redundancy and wordiness
- Ensuring consistent tone and style
- Improving word choice for clarity and impact

Output the improved version of the content directly. Do not include editing notes or track changes -
just provide the polished final text.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var factCheckerAgent = Agent.CreateBuilder(model)
                .WithPersona(@"FactChecker - You are a Fact-Checker and Quality Reviewer. Your job is to review content for accuracy.

Your responsibilities:
- Identify any claims that need verification or caveats
- Add appropriate qualifiers (e.g., 'studies suggest', 'according to experts')
- Flag any potentially outdated information
- Ensure balanced presentation of topics
- Add a brief disclaimer if the content contains opinions vs facts

Output the final content with any necessary accuracy improvements.
Add a brief 'Note:' section at the end if there are important caveats readers should know.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // Create the pipeline orchestrator
            var pipeline = new PipelineOrchestrator()
                .AddStage("Outliner", outlinerAgent)
                .AddStage("Writer", writerAgent)
                .AddStage("Editor", editorAgent)
                .AddStage("FactChecker", factCheckerAgent);

            Console.WriteLine("Pipeline Stages:");
            Console.WriteLine("  1. Outliner  → Creates structured outline");
            Console.WriteLine("  2. Writer    → Expands into full content");
            Console.WriteLine("  3. Editor    → Polishes grammar and style");
            Console.WriteLine("  4. Fact-Checker → Verifies accuracy\n");

            Console.WriteLine("Enter a topic or brief for content creation.");
            Console.WriteLine("Type 'quit' to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Topic: ");
                Console.ResetColor();

                string? topic = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(topic))
                {
                    Console.WriteLine("Please enter a topic.\n");
                    continue;
                }

                if (topic.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              CONTENT CREATION PIPELINE                        ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    int stageNumber = 0;
                    string[] stageNames = { "OUTLINER", "WRITER", "EDITOR", "FACT-CHECKER" };
                    ConsoleColor[] stageColors = { ConsoleColor.Yellow, ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Cyan };

                    var result = await pipeline.ExecuteAsync(
                        $"Create content about: {topic}",
                        cts.Token);

                    foreach (var stageResult in result.AgentResults)
                    {
                        if (stageNumber < stageNames.Length)
                        {
                            Console.WriteLine($"┌─── Stage {stageNumber + 1}: {stageNames[stageNumber]} ───────────────────────────────────────");
                            Console.ForegroundColor = stageColors[stageNumber];
                        }

                        if (stageResult.IsSuccess)
                        {
                            string output = stageResult.Content ?? "";
                            if (stageNumber < stageNames.Length - 1 && output.Length > 500)
                            {
                                Console.WriteLine(output.Substring(0, 500) + "...\n[Output truncated for display]");
                            }
                            else
                            {
                                Console.WriteLine(output);
                            }
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Stage failed: {stageResult.Error?.Message ?? "Unknown error"}");
                        }

                        Console.ResetColor();
                        Console.WriteLine($"└─────────────────────────────────────────────────────────────────\n");
                        stageNumber++;
                    }

                    Console.WriteLine("╔═══════════════════════════════════════════════════════════════╗");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("║                    FINAL CONTENT                              ║");
                    Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝");
                    Console.ResetColor();

                    Console.WriteLine(result.Content ?? "No final output generated.");

                    Console.WriteLine("\n--- Pipeline Complete ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"(Stages completed: {result.AgentResults.Count} | Successful: {result.AgentResults.Count(r => r.IsSuccess)} | Duration: {result.Duration.TotalSeconds:F1}s)");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nPipeline timed out.");
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
    }
}
