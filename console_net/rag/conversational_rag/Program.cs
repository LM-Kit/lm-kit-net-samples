using LMKit.Data;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.Retrieval.Events;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Diagnostics;
using System.Text;

namespace conversational_rag
{
    internal class Program
    {
        const string EmbeddingModelId = "embeddinggemma-300m";

        static bool _isDownloading;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintHeader();

            // ── Step 1: Model selection ─────────────────────────────────────────

            PrintSection("Chat Model Selection");
            Console.WriteLine("  0 - Alibaba Qwen-3 8B       (~6 GB VRAM) [Recommended]");
            Console.WriteLine("  1 - Google Gemma 3 12B       (~9 GB VRAM)");
            Console.WriteLine("  2 - Alibaba Qwen-3 14B       (~10 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B     (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B        (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B    (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.5 27B       (~18 GB VRAM)");
            Console.WriteLine("  *   Or enter a custom model URI or model ID");
            Console.WriteLine();

            LM chatModel = PromptModelSelection();

            Console.Clear();
            PrintHeader();

            PrintSection("Loading Models");
            PrintStatus("Chat model loaded", ConsoleColor.Green);

            LM embeddingModel = LM.LoadFromModelID(
                EmbeddingModelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            PrintStatus($"Embedding model loaded ({EmbeddingModelId})", ConsoleColor.Green);
            Console.WriteLine();

            // ── Step 2: Build a knowledge base ──────────────────────────────────

            PrintSection("Building Knowledge Base");

            var ragEngine = new RagEngine(embeddingModel);

            // Sample knowledge organized by topic.
            // In a real application you would import files, web pages, or database records.
            var knowledgeBase = GetSampleKnowledge();

            var sw = Stopwatch.StartNew();
            int totalChunks = 0;

            foreach (var (topic, content) in knowledgeBase)
            {
                ragEngine.ImportText(
                    content,
                    new TextChunking() { MaxChunkSize = 400 },
                    dataSourceIdentifier: "knowledge",
                    sectionIdentifier: topic);

                var section = ragEngine.DataSources[0].GetSectionByIdentifier(topic);
                int chunkCount = section?.Partitions.Count ?? 0;
                totalChunks += chunkCount;

                PrintStatus($"  Indexed \"{topic}\" ({chunkCount} chunks)", ConsoleColor.DarkGray);
            }

            sw.Stop();
            PrintStatus($"Knowledge base ready: {knowledgeBase.Count} topics, {totalChunks} chunks in {sw.Elapsed.TotalSeconds:F1}s", ConsoleColor.Green);
            Console.WriteLine();

            // ── Step 3: Create RagChat and start conversation ───────────────────

            PrintSection("Starting Conversational RAG");

            using var chat = new RagChat(ragEngine, chatModel);

            // Configure retrieval
            chat.MaxRetrievedPartitions = 3;
            chat.MinRelevanceScore = 0.5f;
            chat.QueryGenerationMode = QueryGenerationMode.Original;

            // Subscribe to events
            chat.RetrievalCompleted += OnRetrievalCompleted;
            chat.AfterTextCompletion += OnAfterTextCompletion;

            PrintStatus("RagChat initialized with contextual query rewriting", ConsoleColor.Cyan);
            Console.WriteLine();
            PrintCommands();
            PrintChatReady();

            string mode = "chat";
            string prompt = "";

            while (true)
            {
                if (mode != "regenerate")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  You: ");
                    Console.ResetColor();

                    string? line = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        break;
                    }

                    prompt = line;
                }

                // Handle commands
                if (TryHandleCommand(prompt, chat, ref mode))
                {
                    continue;
                }

                // Submit question
                try
                {
                    Console.WriteLine();

                    string modeLabel = chat.QueryGenerationMode switch
                    {
                        QueryGenerationMode.Contextual => "Rewriting query",
                        QueryGenerationMode.MultiQuery => "Generating query variants",
                        QueryGenerationMode.HypotheticalAnswer => "Generating hypothetical answer (HyDE)",
                        _ => "Searching knowledge base"
                    };

                    PrintStatus($"{modeLabel} and retrieving relevant context...", ConsoleColor.DarkYellow);

                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
                    var result = chat.Submit(prompt, cts.Token);

                    Console.WriteLine();
                    PrintResponseStats(result);
                    mode = "chat";
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine();
                    PrintStatus("Response timed out", ConsoleColor.Yellow);
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    PrintStatus($"Error: {ex.Message}", ConsoleColor.Red);
                }
            }

            Console.WriteLine();
            PrintDivider();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Demo ended. Press any key to exit.");
            Console.ResetColor();
            Console.ReadKey(true);
        }

        // ─── Event Handlers ────────────────────────────────────────────────────

        private static void OnRetrievalCompleted(object? sender, RetrievalCompletedEventArgs e)
        {
            if (e.RetrievedPartitions.Count == 0)
            {
                PrintStatus("  No relevant partitions found", ConsoleColor.DarkYellow);
            }
            else
            {
                PrintStatus(
                    $"  Retrieved {e.RetrievedPartitions.Count}/{e.RequestedCount} partitions in {e.Elapsed.TotalMilliseconds:F0}ms",
                    ConsoleColor.DarkCyan);

                // Group by section for cleaner display
                var bySections = e.RetrievedPartitions
                    .GroupBy(p => p.SectionIdentifier)
                    .ToList();

                foreach (var group in bySections)
                {
                    float bestScore = group.Max(p => p.Similarity);
                    PrintStatus(
                        $"      {group.Key}: {group.Count()} partition(s), best score {bestScore:F3}",
                        ConsoleColor.DarkGray);
                }
            }

            PrintStatus("Generating response...", ConsoleColor.DarkYellow);
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("  Assistant: ");
            Console.ResetColor();
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
            Console.ResetColor();
        }

        // ─── Command Handling ──────────────────────────────────────────────────

        private static bool TryHandleCommand(string input, RagChat chat, ref string mode)
        {
            string command = input.Trim().ToLowerInvariant();

            switch (command)
            {
                case "/reset":
                    chat.ClearHistory();
                    PrintStatus("Conversation history cleared", ConsoleColor.Yellow);
                    return true;

                case "/mode":
                    PrintSection("Query Generation Modes");
                    Console.WriteLine("  0 - Original         (use question as-is)");
                    Console.WriteLine("  1 - Contextual       (rewrite follow-ups using history)");
                    Console.WriteLine("  2 - Multi-Query      (generate variants, merge results)");
                    Console.WriteLine("  3 - HyDE             (hypothetical answer as query)");
                    Console.Write("\n  Select: ");

                    string? modeInput = Console.ReadLine()?.Trim();
                    chat.QueryGenerationMode = modeInput switch
                    {
                        "0" => QueryGenerationMode.Original,
                        "2" => QueryGenerationMode.MultiQuery,
                        "3" => QueryGenerationMode.HypotheticalAnswer,
                        _ => QueryGenerationMode.Contextual
                    };

                    PrintStatus($"Query mode set to: {chat.QueryGenerationMode}", ConsoleColor.Green);
                    return true;

                case "/topk":
                    Console.Write("  Enter max partitions (current: {0}): ", chat.MaxRetrievedPartitions);
                    if (int.TryParse(Console.ReadLine(), out int topK) && topK > 0)
                    {
                        chat.MaxRetrievedPartitions = topK;
                        PrintStatus($"Max retrieved partitions set to {topK}", ConsoleColor.Green);
                    }

                    return true;

                case "/stats":
                    PrintSection("Current Configuration");
                    PrintStatus($"  Query mode:       {chat.QueryGenerationMode}", ConsoleColor.White);
                    PrintStatus($"  Max partitions:   {chat.MaxRetrievedPartitions}", ConsoleColor.White);
                    PrintStatus($"  Min score:        {chat.MinRelevanceScore:F2}", ConsoleColor.White);
                    PrintStatus($"  Context size:     {chat.ContextSize} tokens", ConsoleColor.White);
                    PrintStatus($"  History turns:    {chat.ChatHistory.MessageCount / 2}", ConsoleColor.White);
                    PrintStatus($"  Data sources:     {chat.Engine.DataSources.Count}", ConsoleColor.White);
                    return true;

                case "/help":
                    PrintCommands();
                    return true;

                default:
                    return false;
            }
        }

        // ─── Model Loading ─────────────────────────────────────────────────────

        private static LM PromptModelSelection()
        {
            Console.Write("  > ");
            string? input = Console.ReadLine();

            string? modelId = input?.Trim() switch
            {
                "0" or "" or null => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            string uri = input!.Trim().Trim('"');

            if (!uri.Contains("://"))
            {
                return LM.LoadFromModelID(
                    uri,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(uri),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
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

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        // ─── Sample Knowledge Base ─────────────────────────────────────────────
        //
        // This uses a FICTIONAL company ("NovaPulse Technologies") with invented
        // names, dates, figures, and product specs. Because none of this exists in
        // the model's training data, every correct answer MUST come from retrieval,
        // making it easy to verify that RAG is working.

        private static List<(string Topic, string Content)> GetSampleKnowledge()
        {
            return new List<(string, string)>
            {
                ("Company Overview", """
                    NovaPulse Technologies is a private deep-tech company specializing in quantum-enhanced
                    navigation systems for the aerospace industry. The company was founded in March 2019 by
                    Dr. Ilyana Vostok and Marcus Thielmann in Zurich, Switzerland. As of January 2026 the
                    company employs 342 people across offices in Zurich (headquarters), Toulouse, and Singapore.

                    The core technology is the Quantum Pulse Positioning (QPP) platform, which uses
                    entangled-photon timing signals to achieve sub-millimeter positioning accuracy in orbit.
                    The QPP platform is protected by 17 granted patents and 9 pending applications.

                    The company completed a Series C funding round of CHF 120 million in September 2025,
                    led by Helios Ventures with participation from ESA's InCubed program. Total funding
                    raised to date stands at CHF 214 million. The company has been profitable since Q2 2025,
                    with an annual revenue run-rate of approximately CHF 195 million.
                    """),

                ("Product Catalog", """
                    Three navigation modules are available, all built on the Quantum Pulse Positioning platform.

                    QNav-100: entry-level module for small satellite constellations (under 500 kg). Provides
                    2.4 mm positioning accuracy, weighs 1.8 kg, consumes 12 watts, rated for orbits up to
                    1,200 km altitude. Unit price: CHF 38,000. Over 400 units delivered since launch in
                    June 2022.

                    QNav-500: commercial-grade module for medium satellites and space stations. Delivers
                    0.9 mm accuracy, supports altitudes up to 36,000 km (geostationary orbit), weighs
                    4.1 kg, consumes 28 watts. Includes a redundant dual-core timing processor and on-board
                    diagnostics suite. Unit price: CHF 145,000.

                    QNav-900X: high-assurance variant for defense and deep-space missions. Achieves 0.3 mm
                    accuracy, operates beyond geostationary orbit including lunar transfer trajectories.
                    Carries MIL-STD-810H and ECSS-Q-ST-60C certifications. Features triple-redundant
                    processors, radiation-hardened electronics, and autonomous fault-recovery. Unit price
                    on request (estimated CHF 400,000+). First deliveries began January 2026 to an
                    undisclosed European defense customer.
                    """),

                ("Employee Handbook", """
                    Paid Time Off: full-time staff receive 27 days of paid vacation per year plus 5 personal
                    days. Unused vacation can be carried over for up to one calendar year. Part-time staff
                    receive vacation pro-rata based on FTE percentage.

                    Remote Work: employees may work from home up to 3 days per week. Wednesdays are mandatory
                    in-office collaboration days. Fully remote arrangements require VP-level approval and
                    exclude roles involving classified projects.

                    Travel and Expenses: business meals are reimbursed up to CHF 65 per person. Hotel stays
                    are capped at CHF 220 per night domestically and CHF 300 internationally. Economy class
                    flights are standard for trips under 6 hours; business class for 6 hours or longer.

                    Performance Reviews: semi-annual cycle in April and October. Ratings use a 5-level scale:
                    Exceptional, Exceeds Expectations, Meets Expectations, Needs Improvement, Unsatisfactory.
                    Annual bonuses range from 5% to 20% of base salary based on individual rating and overall
                    company performance.

                    Equity: staff hired before the September 2025 Series C received stock options under
                    the Equity Incentive Plan (NEIP), vesting over four years with a one-year cliff.
                    Post-Series C hires receive Restricted Stock Units (RSUs) vesting quarterly over three years.
                    """),

                ("Q4 2025 Financial Results", """
                    Fourth-quarter 2025 revenue reached CHF 52.7 million, up 38% year-over-year and 11%
                    quarter-over-quarter. Full-year 2025 revenue totaled CHF 178.3 million versus
                    CHF 112.6 million in 2024.

                    Gross margin for the quarter was 64.2%, up from 59.8% in Q3, driven by higher QNav-500
                    shipment volumes and improved manufacturing yields at the Toulouse facility. Operating
                    expenses were CHF 22.1 million, with R&D at CHF 14.3 million (64.7% of OpEx).

                    Key contracts signed in Q4: a 5-year, CHF 86 million deal with the European Space Agency
                    for the Galileo Next Generation program; CHF 31 million from JAXA for the EarthCARE-2
                    climate monitoring constellation; CHF 12 million pilot with Asia-Pacific telecom operator
                    StarBridge Communications.

                    The Singapore office opened November 4, 2025 with 28 engineers and sales staff. The
                    Asia-Pacific headcount is planned to reach 60 by end of 2026.

                    Cash and equivalents at quarter-end stood at CHF 97.4 million with zero debt.
                    """),

                ("2026 Technical Roadmap", """
                    Two major R&D initiatives are planned for 2026: the QNav-1000 next-generation module
                    and the GroundLink terrestrial positioning system.

                    QNav-1000 replaces the QNav-900X with a unified platform for defense and commercial
                    deep-space missions. Target specs: 0.15 mm accuracy (2x improvement), operation up to
                    2 AU from Earth, 40% lower power consumption via the new Helios-7 quantum timing chip.
                    The Helios-7 is co-developed with ETH Zurich's Quantum Engineering Lab under a
                    CHF 18 million joint research agreement (signed October 2025). Tape-out is scheduled
                    for Q3 2026; engineering samples expected Q1 2027.

                    GroundLink adapts orbital positioning technology for high-precision indoor and urban use
                    where GPS is unreliable. Target: 5 cm accuracy indoors via compact base stations.
                    Primary markets: autonomous warehousing, surgical robotics, autonomous vehicles. A
                    proof-of-concept at the Munich Robotics Expo (December 2025) achieved 3.8 cm over
                    2,000 m². Beta trials with DHL Supply Chain and Kuehne+Nagel start H2 2026.

                    Other milestones: new hardware testing lab in Toulouse (CHF 9 million budget), QPP SDK
                    v3.0 with real-time orbit determination APIs, QPP Integration Guide for third-party
                    satellite bus manufacturers.
                    """)
            };
        }

        // ─── Console Helpers ───────────────────────────────────────────────────

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("  ╔══════════════════════════════════════════════════════════╗");
            Console.WriteLine("  ║             RAG Chat Demo (RagChat)                     ║");
            Console.WriteLine("  ╠══════════════════════════════════════════════════════════╣");
            Console.WriteLine("  ║  Multi-turn Q&A over a fictional company knowledge base ║");
            Console.WriteLine("  ║  using retrieval-augmented generation with RagChat.      ║");
            Console.WriteLine("  ╚══════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintSection(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  ── {title} ──");
            Console.ResetColor();
        }

        private static void PrintStatus(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"  {message}");
            Console.ResetColor();
        }

        private static void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  ────────────────────────────────────────────────────────");
            Console.ResetColor();
        }

        private static void PrintCommands()
        {
            PrintSection("Commands");
            Console.WriteLine("  /reset  - Clear conversation history");
            Console.WriteLine("  /mode   - Change query generation mode");
            Console.WriteLine("  /topk   - Change max retrieved partitions");
            Console.WriteLine("  /stats  - Show current configuration");
            Console.WriteLine("  /help   - Show this help");
            Console.WriteLine("  (empty) - Exit");
        }

        private static void PrintChatReady()
        {
            Console.WriteLine();
            PrintDivider();
            PrintStatus("Ready! The knowledge base contains fictional data about \"NovaPulse Technologies\".", ConsoleColor.Green);
            PrintStatus("Try: \"What products does NovaPulse sell?\" or \"How many vacation days do employees get?\"", ConsoleColor.DarkGray);
            PrintStatus("The model cannot know these facts from training, so correct answers prove RAG is working.", ConsoleColor.DarkGray);
            PrintStatus("Follow-up questions are automatically contextualized.", ConsoleColor.DarkGray);
        }

        private static void PrintResponseStats(RagQueryResult result)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;

            var response = result.Response;
            int contextTokens = response.ContextSize;
            int partitionCount = result.RetrievedPartitions?.Count ?? 0;

            Console.WriteLine(
                $"  [{partitionCount} partitions | " +
                $"{contextTokens} ctx tokens | " +
                $"quality: {response.QualityScore:F2}]");

            Console.ResetColor();
        }
    }
}
