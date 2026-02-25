using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Model;
using System.Text;

namespace email_triage_agent
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
            Console.WriteLine("=== Email Triage & Response Agent Demo ===\n");
            Console.WriteLine("This demo showcases supervisor-based orchestration for email processing.");
            Console.WriteLine("A supervisor agent delegates to specialist workers:\n");
            Console.WriteLine("  - Classifier:  Categorizes email by type and urgency");
            Console.WriteLine("  - Extractor:   Pulls key information and requested actions");
            Console.WriteLine("  - Drafter:     Composes a professional response\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
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
            Console.WriteLine("=== Email Triage & Response Agent ===\n");

            // Create specialist worker agents
            var classifierAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Email Classifier - You are an expert email triage specialist. Your job is to analyze incoming
emails and classify them along multiple dimensions.

For each email, provide:
- Category: [Support Request | Sales Inquiry | Complaint | Bug Report | Feature Request | Internal | Billing | Partnership | Spam | Other]
- Urgency: [Critical | High | Normal | Low]
- Sentiment: [Positive | Neutral | Negative | Angry]
- Requires Response: [Yes | No]
- Escalation Needed: [Yes | No] (Yes if angry customer, legal threat, security issue, or VIP)

Format your output as a structured classification report. Be precise and consistent.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var extractorAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Email Information Extractor - You are an expert at extracting structured information from emails.

For each email, extract:
- Sender Intent: What does the sender want? (1-2 sentences)
- Key Questions: List any explicit questions asked
- Requested Actions: What specific actions are being requested?
- Deadlines: Any mentioned dates or timeframes
- Reference Numbers: Order IDs, ticket numbers, account numbers, etc.
- People Mentioned: Names and their roles/context
- Technical Details: Product names, versions, error codes, etc.

Present the extraction in a clear, structured format. Only include fields that have relevant content.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var drafterAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Email Response Drafter - You are a professional customer communications specialist.
Your job is to draft appropriate email responses based on the email classification and extracted information.

Guidelines:
- Match tone to context: empathetic for complaints, enthusiastic for sales, helpful for support
- Address ALL questions and requested actions from the original email
- Include specific next steps with realistic timeframes
- Reference any order numbers, ticket IDs, or other details from the original
- Keep responses concise but thorough (150-300 words)
- For complaints: acknowledge the issue, apologize, provide resolution path
- For support: provide clear steps or acknowledge investigation needed
- For sales: show enthusiasm, highlight relevant features, suggest next steps
- Sign off professionally

Output format:
Subject: Re: [original subject or appropriate subject]
Body:
[The full email response ready to send]")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // Create supervisor agent
            var supervisorAgent = Agent.CreateBuilder(model)
                .WithPersona(@"Email Triage Supervisor - You coordinate the email processing workflow.
When given an email to process, delegate tasks to your specialist workers in order:

1. First, delegate to the Classifier to categorize the email
2. Then, delegate to the Extractor to pull key information
3. Finally, delegate to the Drafter to compose an appropriate response

After all workers complete, provide a brief triage summary combining all results.")
                .WithPlanning(PlanningStrategy.ChainOfThought)
                .Build();

            // Create supervisor orchestrator
            var supervisor = new SupervisorOrchestrator(supervisorAgent)
                .AddWorker(classifierAgent)
                .AddWorker(extractorAgent)
                .AddWorker(drafterAgent);

            Console.WriteLine("Paste or type an email to process (end with an empty line).");
            Console.WriteLine("You can also type a single-line request like 'Process this email: ...'");
            Console.WriteLine("Type 'sample' to use a built-in sample email.");
            Console.WriteLine("Type 'quit' to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Email: ");
                Console.ResetColor();

                string? firstLine = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(firstLine))
                    continue;

                if (firstLine.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                string emailContent;
                if (firstLine.Equals("sample", StringComparison.OrdinalIgnoreCase))
                {
                    emailContent = GetSampleEmail();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("\n--- Sample Email ---");
                    Console.WriteLine(emailContent);
                    Console.WriteLine("--- End of Sample ---\n");
                    Console.ResetColor();
                }
                else
                {
                    // Read multi-line email input
                    var emailBuilder = new StringBuilder(firstLine);
                    while (true)
                    {
                        string? line = Console.ReadLine();
                        if (string.IsNullOrEmpty(line))
                            break;
                        emailBuilder.AppendLine();
                        emailBuilder.Append(line);
                    }
                    emailContent = emailBuilder.ToString().Trim();
                }

                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              EMAIL TRIAGE & RESPONSE                          ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

                    var result = await supervisor.RunStreamingAsync(
                        $"Process the following email and provide classification, extraction, and a draft response:\n\n{emailContent}",
                        new DelegateOrchestrationStreamHandler(
                            onStart: () =>
                            {
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.WriteLine("Starting email triage...\n");
                                Console.ResetColor();
                            },
                            onToken: token =>
                            {
                                Console.ForegroundColor = token.Type switch
                                {
                                    OrchestrationStreamTokenType.Delegation => ConsoleColor.Yellow,
                                    OrchestrationStreamTokenType.AgentStarted => ConsoleColor.Cyan,
                                    OrchestrationStreamTokenType.AgentCompleted => ConsoleColor.Green,
                                    OrchestrationStreamTokenType.Thinking => ConsoleColor.Blue,
                                    OrchestrationStreamTokenType.ToolCall => ConsoleColor.Magenta,
                                    OrchestrationStreamTokenType.ToolResult => ConsoleColor.DarkMagenta,
                                    OrchestrationStreamTokenType.Error => ConsoleColor.Red,
                                    _ => ConsoleColor.White
                                };

                                switch (token.Type)
                                {
                                    case OrchestrationStreamTokenType.Delegation:
                                        Console.WriteLine($"\n┌─── Delegating to: {token.AgentName} ───────────────────────");
                                        break;
                                    case OrchestrationStreamTokenType.AgentCompleted:
                                        Console.ResetColor();
                                        Console.WriteLine($"\n└─── {token.AgentName} completed ───────────────────────────\n");
                                        break;
                                    case OrchestrationStreamTokenType.Content:
                                        Console.Write(token.Text);
                                        break;
                                    case OrchestrationStreamTokenType.Status:
                                        Console.WriteLine($"  [{token.Text}]");
                                        break;
                                }

                                Console.ResetColor();
                            },
                            onComplete: () =>
                            {
                                Console.WriteLine();
                            },
                            onError: ex =>
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"\nError: {ex.Message}");
                                Console.ResetColor();
                            }),
                        cancellationToken: cts.Token);

                    Console.WriteLine("\n--- Email Triage Complete ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"(Workers used: {result.AgentResults.Count} | Successful: {result.AgentResults.Count(r => r.IsSuccess)} | Duration: {result.Duration.TotalSeconds:F1}s)");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nProcessing timed out.");
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

        static string GetSampleEmail()
        {
            return @"From: sarah.chen@acmecorp.com
To: support@yourcompany.com
Subject: URGENT - Order #ORD-2024-5847 delivered damaged, need replacement ASAP

Hi Support Team,

I'm writing regarding order #ORD-2024-5847 placed on January 15th for 50 units of the
ProWidget X500 for our Chicago office. The shipment arrived today and unfortunately
12 of the 50 units have visible damage to the outer casing, and 3 units won't power on at all.

This is extremely frustrating as we have a product launch event scheduled for February 3rd
and we need all 50 units operational by then. We've been a loyal customer for 3 years
and have never experienced quality issues like this before.

Could you please:
1. Arrange replacement of the 15 damaged/defective units
2. Provide expedited shipping to arrive before February 1st
3. Send a return label for the damaged units
4. Apply a discount to our next order given the inconvenience

Our account manager is James Rivera (james.r@yourcompany.com) - please loop him in.

I need confirmation of the replacement timeline by end of day tomorrow.

Best regards,
Sarah Chen
VP of Operations, Acme Corp
Phone: (312) 555-0198";
        }
    }
}
