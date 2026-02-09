using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Model;
using System.Text;

namespace smart_task_router
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
        static readonly string DEFAULT_GLM_4_7_FLASH_MODEL_PATH = @"https://huggingface.co/lm-kit/glm-4.7-flash-gguf/resolve/main/GLM-4.7-Flash-64x2.6B-Q4_K_M.gguf";

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

        private static async Task Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Smart Task Router Demo ===\n");
            Console.WriteLine("This demo showcases supervisor-based task delegation.");
            Console.WriteLine("A coordinator analyzes requests and routes to specialists.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("1 - Microsoft Phi-4 Mini 3.8B (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("2 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("3 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("4 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("5 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("6 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink = input?.Trim() switch
            {
                "0" => DEFAULT_GEMMA3_4B_MODEL_PATH,
                "1" => DEFAULT_PHI4_MINI_3_8B_MODEL_PATH,
                "2" => DEFAULT_LLAMA3_1_8B_MODEL_PATH,
                "3" => DEFAULT_QWEN3_8B_MODEL_PATH,
                "4" => DEFAULT_PHI4_14_7B_MODEL_PATH,
                "5" => DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH,
                "6" => DEFAULT_GLM_4_7_FLASH_MODEL_PATH,
                _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : DEFAULT_GEMMA3_4B_MODEL_PATH
            };

            // Load model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.WriteLine("=== Smart Task Router ===\n");

            // Create specialized worker agents
            var codeExpert = Agent.CreateBuilder(model)
                .WithPersona(@"CodeExpert - You are an expert programmer and software developer.
Your specialties include:
- Writing clean, efficient code in multiple languages
- Debugging and code review
- Explaining programming concepts
- Suggesting best practices and design patterns

When given a coding task, provide working code with clear comments.
When explaining concepts, use code examples to illustrate.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            var dataAnalyst = Agent.CreateBuilder(model)
                .WithPersona(@"DataAnalyst - You are an expert data analyst with strong statistical skills.
Your specialties include:
- Analyzing numerical data and identifying trends
- Creating summaries and insights from data
- Statistical analysis and interpretation
- Data visualization recommendations

When analyzing data, provide clear insights, identify patterns, and make actionable recommendations.
Use tables and structured formats to present findings clearly.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            var writer = Agent.CreateBuilder(model)
                .WithPersona(@"Writer - You are a professional technical writer and content creator.
Your specialties include:
- Writing clear documentation and guides
- Creating engaging articles and explanations
- Summarizing complex topics for various audiences
- Editing and improving written content

Write in a clear, engaging style appropriate for the target audience.
Structure content logically with headers and bullet points where appropriate.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var researcher = Agent.CreateBuilder(model)
                .WithPersona(@"Researcher - You are a thorough researcher and knowledge synthesizer.
Your specialties include:
- Deep diving into topics to gather comprehensive information
- Comparing and contrasting different approaches
- Synthesizing information from multiple perspectives
- Providing balanced, well-researched answers

When researching, be thorough but concise. Present multiple viewpoints when relevant.
Cite the basis for your conclusions.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            // Create the supervisor agent
            var supervisorAgent = Agent.CreateBuilder(model)
                .WithPersona(@"You are a Task Coordinator responsible for routing user requests to the right specialists.

Available specialists:
- CodeExpert: Programming, code writing, debugging, software development
- DataAnalyst: Data analysis, statistics, trends, metrics interpretation
- Writer: Documentation, content creation, explanations, editing
- Researcher: Information gathering, comparisons, deep dives, learning

For each user request:
1. Analyze what type of expertise is needed
2. Delegate to the most appropriate specialist(s)
3. If a task needs multiple skills, delegate to each relevant specialist
4. Combine responses into a coherent final answer

Always explain your routing decision briefly before delegating.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            // Create the supervisor orchestrator
            var supervisor = new SupervisorOrchestrator(supervisorAgent)
                .AddWorker(codeExpert)
                .AddWorker(dataAnalyst)
                .AddWorker(writer)
                .AddWorker(researcher);

            // Set up event handlers to show delegation in real-time
            supervisor.BeforeAgentExecution += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n>> Executing agent...");
                var inputPreview = e.OriginalInput.Length > 100 ? e.OriginalInput.Substring(0, 100) + "..." : e.OriginalInput;
                Console.WriteLine($"   Input: {inputPreview}");
                Console.ResetColor();
            };

            supervisor.AfterAgentExecution += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"\n<< Agent completed (Status: {e.Result.Status})");
                Console.ResetColor();
            };

            Console.WriteLine("Available Specialists:");
            Console.WriteLine("  - CodeExpert   : Programming and development");
            Console.WriteLine("  - DataAnalyst  : Data analysis and insights");
            Console.WriteLine("  - Writer       : Documentation and content");
            Console.WriteLine("  - Researcher   : Research and synthesis\n");

            Console.WriteLine("Enter any request. The supervisor will route it appropriately.");
            Console.WriteLine("Complex requests may be split across multiple specialists.");
            Console.WriteLine("Type 'quit' to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Request: ");
                Console.ResetColor();

                string? request = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(request))
                {
                    Console.WriteLine("Please enter a request.\n");
                    continue;
                }

                if (request.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                Console.WriteLine("\n--- Supervisor Analyzing Request ---\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                    // Execute with the supervisor orchestrator
                    var result = await supervisor.ExecuteAsync(request, cts.Token);

                    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine("FINAL RESPONSE");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.ResetColor();
                    Console.WriteLine(result.Content);

                    Console.WriteLine("\n--- Task Complete ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"(Agents involved: {result.AgentResults.Count} | Success: {result.Success})");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nRequest timed out.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\nThank you for using Smart Task Router. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
