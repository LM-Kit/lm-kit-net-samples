using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Model;
using System.Text;

namespace multi_agent_document_review
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";

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
            Console.WriteLine("=== Multi-Agent Document Review Demo ===\n");
            Console.WriteLine("This demo showcases parallel multi-agent orchestration.");
            Console.WriteLine("Three specialized agents review your document simultaneously.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("1 - Microsoft Phi-4 Mini 3.8B (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("2 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("3 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("4 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink = input?.Trim() switch
            {
                "0" => DEFAULT_GEMMA3_4B_MODEL_PATH,
                "1" => DEFAULT_PHI4_MINI_3_8B_MODEL_PATH,
                "2" => DEFAULT_LLAMA3_1_8B_MODEL_PATH,
                "3" => DEFAULT_QWEN3_8B_MODEL_PATH,
                "4" => DEFAULT_PHI4_14_7B_MODEL_PATH,
                _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : DEFAULT_GEMMA3_4B_MODEL_PATH
            };

            // Load model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.WriteLine("=== Multi-Agent Document Review ===\n");

            // Create three specialized reviewer agents
            var technicalReviewer = Agent.CreateBuilder(model)
                .WithPersona(@"Technical Reviewer - You are a Senior Technical Reviewer with 15+ years of software architecture experience.
Your role is to evaluate proposals from a technical perspective. Focus on:
- Technical feasibility and architecture soundness
- Scalability and performance implications
- Security considerations
- Implementation complexity and risks
- Technology choices and alternatives

Provide a structured review with: Technical Assessment, Risks, and Recommendations.
Be specific and cite technical reasons for your concerns.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            var businessAnalyst = Agent.CreateBuilder(model)
                .WithPersona(@"Business Analyst - You are a Business Analyst with expertise in ROI analysis and strategic planning.
Your role is to evaluate proposals from a business perspective. Focus on:
- Return on investment (ROI) and cost-benefit analysis
- Market impact and competitive advantage
- Resource requirements (budget, personnel, time)
- Business risks and mitigation strategies
- Alignment with business objectives

Provide a structured review with: Business Impact, Resource Analysis, and Recommendations.
Use concrete metrics and business reasoning.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            var complianceReviewer = Agent.CreateBuilder(model)
                .WithPersona(@"Compliance Reviewer - You are a Compliance and Risk Officer with expertise in regulatory requirements.
Your role is to evaluate proposals from a compliance perspective. Focus on:
- Data privacy regulations (GDPR, CCPA, etc.)
- Industry-specific compliance requirements
- Security and audit requirements
- Legal risks and liabilities
- Documentation and governance needs

Provide a structured review with: Compliance Assessment, Risk Areas, and Required Actions.
Be specific about which regulations apply.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            // Create parallel orchestrator
            var orchestrator = new ParallelOrchestrator()
                .AddAgent("Technical", technicalReviewer)
                .AddAgent("Business", businessAnalyst)
                .AddAgent("Compliance", complianceReviewer);

            Console.WriteLine("Review Panel Ready:");
            Console.WriteLine("  - Technical Reviewer (Architecture & Implementation)");
            Console.WriteLine("  - Business Analyst (ROI & Strategy)");
            Console.WriteLine("  - Compliance Reviewer (Risk & Regulations)\n");

            Console.WriteLine("Enter the document/proposal to review (end with an empty line):");
            Console.WriteLine("Type 'quit' to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Document: ");
                Console.ResetColor();

                // Read multi-line input
                var documentBuilder = new StringBuilder();
                string? line;
                while (!string.IsNullOrEmpty(line = Console.ReadLine()))
                {
                    if (line.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nThank you for using Multi-Agent Document Review. Press any key to exit.");
                        Console.ReadKey();
                        return;
                    }
                    documentBuilder.AppendLine(line);
                }

                string document = documentBuilder.ToString().Trim();
                if (string.IsNullOrWhiteSpace(document))
                {
                    Console.WriteLine("Please enter a document to review.\n");
                    continue;
                }

                Console.WriteLine("\n--- Starting Parallel Review ---\n");
                Console.WriteLine("All three reviewers are analyzing the document simultaneously...\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

                    // Execute parallel review
                    var reviewPrompt = $"Please review the following proposal/document and provide your expert assessment:\n\n{document}";
                    var results = await orchestrator.ExecuteAsync(reviewPrompt, cts.Token);

                    // Display individual reviews
                    if (results.AgentResults.Count >= 1)
                    {
                        Console.WriteLine("═══════════════════════════════════════════════════════════════");
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("TECHNICAL REVIEW");
                        Console.WriteLine("═══════════════════════════════════════════════════════════════");
                        Console.ResetColor();
                        Console.WriteLine(results.AgentResults[0].Content);
                    }

                    if (results.AgentResults.Count >= 2)
                    {
                        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("BUSINESS ANALYSIS");
                        Console.WriteLine("═══════════════════════════════════════════════════════════════");
                        Console.ResetColor();
                        Console.WriteLine(results.AgentResults[1].Content);
                    }

                    if (results.AgentResults.Count >= 3)
                    {
                        Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine("COMPLIANCE REVIEW");
                        Console.WriteLine("═══════════════════════════════════════════════════════════════");
                        Console.ResetColor();
                        Console.WriteLine(results.AgentResults[2].Content);
                    }

                    // Display aggregated summary
                    Console.WriteLine("\n═══════════════════════════════════════════════════════════════");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("SUMMARY");
                    Console.WriteLine("═══════════════════════════════════════════════════════════════");
                    Console.ResetColor();

                    if (!string.IsNullOrWhiteSpace(results.Content))
                    {
                        Console.WriteLine(results.Content);
                    }
                    else
                    {
                        // Display summary stats
                        Console.WriteLine("Review completed by all three specialists.");
                        Console.WriteLine($"Total reviews: {results.AgentResults.Count}");
                        Console.WriteLine($"Successful: {results.AgentResults.Count(r => r.IsSuccess)}");
                    }

                    Console.WriteLine("\n--- Review Complete ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"(Success: {results.Success} | Duration: {results.Duration.TotalSeconds:F1}s)");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nReview timed out.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
    }
}
