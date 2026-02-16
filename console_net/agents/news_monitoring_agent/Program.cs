using LMKit.Agents;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Agents.Tools.BuiltIn.Net;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace news_monitoring_agent
{
    internal class Program
    {
        static readonly string DEFAULT_GEMMA3_12B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-12b-instruct-lmk/resolve/main/gemma-3-12b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
        static readonly string DEFAULT_GLM_4_7_FLASH_MODEL_PATH = @"https://huggingface.co/lm-kit/glm-4.7-flash-gguf/resolve/main/GLM-4.7-Flash-64x2.6B-Q4_K_M.gguf";

        // Sample RSS feeds for quick selection
        static readonly (string Name, string Url)[] SAMPLE_FEEDS = new[]
        {
            ("Hacker News", "https://hnrss.org/frontpage"),
            ("TechCrunch", "https://techcrunch.com/feed/"),
            ("Ars Technica", "https://feeds.arstechnica.com/arstechnica/index"),
            ("The Verge", "https://www.theverge.com/rss/index.xml"),
            ("BBC News - World", "https://feeds.bbci.co.uk/news/world/rss.xml"),
            ("Reuters - World", "https://www.reutersagency.com/feed/"),
        };

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
            // Set your license key here if you have one
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== News Monitoring Agent ===\n");
            Console.WriteLine("An AI agent that fetches, searches, and summarizes RSS/Atom news feeds.");
            Console.WriteLine("Combines RssFeedTool with WebSearchTool for comprehensive news analysis.\n");

            // Model selection
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("1 - Microsoft Phi-4 Mini 3.8B (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string modelLink = input?.Trim() switch
            {
                "0" => DEFAULT_GEMMA3_12B_MODEL_PATH,
                "1" => DEFAULT_PHI4_MINI_3_8B_MODEL_PATH,
                "2" => DEFAULT_QWEN3_8B_MODEL_PATH,
                "3" => DEFAULT_PHI4_14_7B_MODEL_PATH,
                "4" => DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH,
                "5" => DEFAULT_GLM_4_7_FLASH_MODEL_PATH,
                _ => !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : DEFAULT_GEMMA3_12B_MODEL_PATH
            };

            // Load model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.WriteLine("=== News Monitoring Agent ===\n");

            // Feed selection
            Console.WriteLine("Select RSS feeds to monitor (comma-separated, or press Enter for all):\n");
            for (int i = 0; i < SAMPLE_FEEDS.Length; i++)
            {
                Console.WriteLine($"  {i} - {SAMPLE_FEEDS[i].Name}");
            }
            Console.WriteLine($"  {SAMPLE_FEEDS.Length} - Custom feed URL");
            Console.Write("\n> ");

            string? feedSelection = Console.ReadLine();
            List<string> selectedFeeds = new();
            string feedListDescription;

            if (string.IsNullOrWhiteSpace(feedSelection))
            {
                // Use all default feeds
                foreach (var feed in SAMPLE_FEEDS)
                {
                    selectedFeeds.Add(feed.Url);
                }
                feedListDescription = string.Join(", ", SAMPLE_FEEDS.Select(f => f.Name));
            }
            else
            {
                var names = new List<string>();
                foreach (string part in feedSelection.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (int.TryParse(part, out int idx) && idx >= 0 && idx < SAMPLE_FEEDS.Length)
                    {
                        selectedFeeds.Add(SAMPLE_FEEDS[idx].Url);
                        names.Add(SAMPLE_FEEDS[idx].Name);
                    }
                    else if (int.TryParse(part, out int custom) && custom == SAMPLE_FEEDS.Length)
                    {
                        Console.Write("Enter custom feed URL: ");
                        string? customUrl = Console.ReadLine();
                        if (!string.IsNullOrWhiteSpace(customUrl))
                        {
                            selectedFeeds.Add(customUrl.Trim());
                            names.Add("Custom");
                        }
                    }
                }

                if (selectedFeeds.Count == 0)
                {
                    selectedFeeds.Add(SAMPLE_FEEDS[0].Url);
                    names.Add(SAMPLE_FEEDS[0].Name);
                }

                feedListDescription = string.Join(", ", names);
            }

            Console.Clear();
            Console.WriteLine("=== News Monitoring Agent ===\n");
            Console.WriteLine("Monitoring feeds: {0}\n", feedListDescription);

            // Build the feed URL list for the system prompt
            string feedUrls = string.Join("\n", selectedFeeds.Select(url =>
            {
                var match = SAMPLE_FEEDS.FirstOrDefault(f => f.Url == url);
                return match.Name != null ? $"- {match.Name}: {url}" : $"- {url}";
            }));

            // Create the news monitoring agent with RssFeedTool and WebSearchTool
            MultiTurnConversation chat = new(model);
            chat.MaximumCompletionTokens = 2048;
            chat.SamplingMode = new RandomSampling { Temperature = 0.6f };
            chat.SystemPrompt = $@"You are a news monitoring assistant. You have access to RSS feed tools and web search tools.

Your monitored feeds:
{feedUrls}

Your capabilities:
1. **Fetch feeds**: Use the rss_feed tool with action ""fetch"" to get the latest entries from a feed URL.
2. **Search feeds**: Use the rss_feed tool with action ""search"" to find entries matching a keyword or published after a date.
3. **Web search**: Use the web_search tool to find additional context or breaking news beyond the RSS feeds.

Guidelines:
- When asked for a news briefing, fetch the top entries from the monitored feeds and provide a concise summary.
- When asked about a specific topic, search relevant feeds by keyword and supplement with web search if needed.
- Always cite the source name and publication date when summarizing articles.
- Keep summaries concise: 1-2 sentences per article unless asked for more detail.
- When comparing coverage across sources, fetch from multiple feeds and highlight differences.";

            // Register the RSS feed tool and web search tool
            chat.Tools.Register(BuiltInTools.RssFetch);
            chat.Tools.Register(BuiltInTools.WebSearch);
            chat.Tools.Register(BuiltInTools.DateTimeNow);

            chat.AfterTextCompletion += (sender, e) =>
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
            };

            // Show available commands
            Console.WriteLine("Commands:");
            Console.WriteLine("  /briefing  - Get a quick news briefing from all monitored feeds");
            Console.WriteLine("  /feeds     - Show monitored feed URLs");
            Console.WriteLine("  /reset     - Clear chat history");
            Console.WriteLine("  Type any question to search and analyze news\n");

            const string FIRST_MESSAGE = "Greet the user and briefly describe what you can do. Mention the monitored feeds by name.";
            string prompt = FIRST_MESSAGE;

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                CancellationTokenSource cts = new(TimeSpan.FromMinutes(3));

                try
                {
                    var result = chat.Submit(prompt, cts.Token);

                    Console.Write("\n(gen. tokens: {0} - stop reason: {1} - speed: {2} tok/s - ctx: {3}/{4})",
                        result.GeneratedTokens.Count,
                        result.TerminationReason,
                        Math.Round(result.TokenGenerationRate, 2),
                        result.ContextTokens.Count,
                        result.ContextSize);
                }
                catch (OperationCanceledException)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("\nRequest timed out.");
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

                if (string.Compare(prompt, "/reset", true) == 0)
                {
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
                }
                else if (string.Compare(prompt, "/briefing", true) == 0)
                {
                    prompt = "Give me a quick news briefing. Fetch the latest entries from each monitored feed and summarize the top 3 stories from each.";
                }
                else if (string.Compare(prompt, "/feeds", true) == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("\nMonitored feeds:");
                    foreach (var url in selectedFeeds)
                    {
                        var match = SAMPLE_FEEDS.FirstOrDefault(f => f.Url == url);
                        Console.WriteLine("  {0}: {1}", match.Name ?? "Custom", url);
                    }
                    Console.ResetColor();
                    prompt = " ";
                    continue;
                }
            }

            Console.WriteLine("Goodbye. Press any key to exit.");
            Console.ReadKey();
        }
    }
}
