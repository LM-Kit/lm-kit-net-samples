using LMKit.Agents;
using LMKit.Agents.Memory;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Text;

namespace persistent_memory_assistant
{
    internal class Program
    {
        static readonly string MEMORY_FILE_PATH = "./agent_memory.bin";

        static bool _isDownloading;

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            string modelName = path.Contains("embeddinggemma") ? "embedding model" : "chat model";
            if (contentLength.HasValue)
                Console.Write($"\rDownloading {modelName} {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading {modelName} {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
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

        private static Agent CreateAgent(LM model, AgentMemory memory)
        {
            return Agent.CreateBuilder(model)
                .WithPersona("You are a helpful, conversational personal assistant.")
                .WithPlanning(PlanningStrategy.None)
                .WithMemory(memory)
                .Build();
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Persistent Memory Assistant Demo ===\n");
            Console.WriteLine("This demo showcases built-in automatic memory extraction.");
            Console.WriteLine("The assistant remembers information you share and recalls it in future sessions.\n");

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

            Console.WriteLine("\nLoading models (chat + embedding for memory)...\n");

            // Load chat model
            LM model = LoadModel(input ?? "");
            Console.WriteLine();

            // Load embedding model for memory
            var embeddingModel = LM.LoadFromModelID("embeddinggemma-300m",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            Console.Clear();
            Console.WriteLine("=== Persistent Memory Assistant ===\n");

            // Create or load agent memory
            AgentMemory memory;

            if (File.Exists(MEMORY_FILE_PATH))
            {
                try
                {
                    memory = AgentMemory.Deserialize(MEMORY_FILE_PATH, embeddingModel);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Loaded {memory.DataSources.Count} memory sources from previous session.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Could not load previous memories: {ex.Message}");
                    Console.ResetColor();
                    memory = new AgentMemory(embeddingModel);
                }
            }
            else
            {
                memory = new AgentMemory(embeddingModel);
            }

            // Enable built-in LLM-based memory extraction.
            memory.ExtractionMode = MemoryExtractionMode.LlmBased;
            memory.RunExtractionSynchronously = true;

            // Configure capacity limits
            memory.MaxMemoryEntries = 100;
            memory.EvictionPolicy = MemoryEvictionPolicy.OldestFirst;

            // Enable time-decay
            memory.TimeDecayHalfLife = TimeSpan.FromDays(30);

            memory.BeforeMemoryStored += (sender, e) =>
            {
                foreach (var mem in e.Memories)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  [Memory extracted: {mem.Text} ({mem.MemoryType}, {mem.Importance})]");
                    Console.ResetColor();
                }
            };

            memory.MemoryEvicted += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Console.WriteLine($"  [Memory evicted: \"{e.Text}\" from {e.DataSourceIdentifier}]");
                Console.ResetColor();
            };

            memory.BeforeMemoryConsolidated += (sender, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  [Consolidating {e.OriginalEntries.Count} entries in {e.DataSourceIdentifier}]");
                Console.WriteLine($"  [Into: {e.ConsolidatedText}]");
                Console.ResetColor();
            };

            var agent = CreateAgent(model, memory);

            var executor = new AgentExecutor();
            executor.AfterTextCompletion += OnAfterTextCompletion;

            ShowHelp();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("\nYou: ");
                Console.ResetColor();

                string? userInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(userInput))
                    continue;

                // Handle commands
                if (userInput.StartsWith("/"))
                {
                    if (userInput.Equals("/new", StringComparison.OrdinalIgnoreCase))
                    {
                        agent = CreateAgent(model, memory);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Started a new chat session. Conversation history cleared, memories preserved.");
                        Console.ResetColor();
                        continue;
                    }

                    HandleCommand(userInput, memory, model, embeddingModel, executor);
                    continue;
                }

                if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    if (memory.DataSources.Count > 0)
                    {
                        Console.WriteLine("\nSaving memories...");
                        memory.Serialize(MEMORY_FILE_PATH);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Saved {memory.DataSources.Count} memory sources to {MEMORY_FILE_PATH}");
                        Console.ResetColor();
                    }
                    break;
                }

                try
                {
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nAssistant: ");
                    Console.ResetColor();

                    var result = executor.Execute(
                        agent,
                        userInput,
                        cancellationToken: cts.Token);

                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nResponse timed out.");
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

        private static void ShowHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  /new             - Start a new chat (clears history, keeps memories)");
            Console.WriteLine("  /remember <info> - Store information in memory");
            Console.WriteLine("  /memories        - List all stored memory entries with timestamps");
            Console.WriteLine("  /capacity <n>    - Set max memory entries (0 = unlimited)");
            Console.WriteLine("  /decay <days>    - Set time-decay half-life in days (0 = off)");
            Console.WriteLine("  /consolidate     - Merge similar memories using LLM summarization");
            Console.WriteLine("  /summarize       - Summarize current conversation into episodic memory");
            Console.WriteLine("  /clear           - Clear all memories");
            Console.WriteLine("  /save            - Save memories to disk");
            Console.WriteLine("  /load            - Load memories from disk");
            Console.WriteLine("  /help            - Show this help");
            Console.WriteLine("  quit             - Exit (auto-saves)\n");
            Console.WriteLine("Chat naturally. The assistant automatically extracts and remembers facts!");
        }

        private static void HandleCommand(string command, AgentMemory memory, LM chatModel, LM embeddingModel, AgentExecutor executor)
        {
            var parts = command.Split(' ', 2);
            var cmd = parts[0].ToLowerInvariant();
            var arg = parts.Length > 1 ? parts[1] : "";

            switch (cmd)
            {
                case "/remember":
                    if (string.IsNullOrWhiteSpace(arg))
                    {
                        Console.WriteLine("Usage: /remember <information to store>");
                        return;
                    }
                    string sectionId = $"user_memory_{DateTime.Now.Ticks}";
                    memory.SaveInformation("user_memories", arg, sectionId, MemoryType.Semantic);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Stored in memory: \"{arg}\"");
                    Console.ResetColor();
                    break;

                case "/memories":
                    var dataSources = memory.DataSources;
                    if (dataSources.Count == 0)
                    {
                        Console.WriteLine("No memories stored yet.");
                        return;
                    }
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    string capacityStr = memory.MaxMemoryEntries > 0
                        ? $"{memory.EntryCount} / {memory.MaxMemoryEntries}"
                        : $"{memory.EntryCount} (unlimited)";
                    Console.WriteLine($"Memory entries: {capacityStr}");
                    Console.ResetColor();
                    foreach (var ds in dataSources)
                    {
                        var memType = AgentMemory.GetMemoryType(ds);
                        Console.WriteLine($"  [{memType}] {ds.Identifier}:");
                        foreach (var section in ds.Sections)
                        {
                            string text = section.Partitions.Count > 0
                                ? section.Partitions[0].Payload
                                : "(empty)";
                            if (text.Length > 60)
                                text = text.Substring(0, 57) + "...";

                            string ts = "";
                            if (section.Metadata.TryGetValue("created_at", out string createdAt) &&
                                DateTime.TryParse(createdAt, out DateTime dt))
                            {
                                ts = $" ({dt.ToLocalTime():g})";
                            }

                            Console.WriteLine($"    - {text}{ts}");
                        }
                    }
                    break;

                case "/capacity":
                    if (int.TryParse(arg, out int cap) && cap >= 0)
                    {
                        memory.MaxMemoryEntries = cap;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Max memory entries set to {(cap == 0 ? "unlimited" : cap.ToString())}.");
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine($"Current capacity: {memory.EntryCount} / {(memory.MaxMemoryEntries > 0 ? memory.MaxMemoryEntries.ToString() : "unlimited")}");
                        Console.WriteLine("Usage: /capacity <number> (0 for unlimited)");
                    }
                    break;

                case "/decay":
                    if (double.TryParse(arg, out double days) && days >= 0)
                    {
                        memory.TimeDecayHalfLife = days > 0 ? TimeSpan.FromDays(days) : TimeSpan.Zero;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(days > 0
                            ? $"Time-decay half-life set to {days} day(s)."
                            : "Time-decay disabled.");
                        Console.ResetColor();
                    }
                    else
                    {
                        string current = memory.TimeDecayHalfLife > TimeSpan.Zero
                            ? $"{memory.TimeDecayHalfLife.TotalDays} day(s)"
                            : "off";
                        Console.WriteLine($"Current time-decay half-life: {current}");
                        Console.WriteLine("Usage: /decay <days> (0 to disable)");
                    }
                    break;

                case "/clear":
                    memory.Clear();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("All memories cleared.");
                    Console.ResetColor();
                    break;

                case "/save":
                    try
                    {
                        memory.Serialize(MEMORY_FILE_PATH);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Saved {memory.DataSources.Count} memory sources to {MEMORY_FILE_PATH}");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error saving: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "/load":
                    try
                    {
                        var loadedMemory = AgentMemory.Deserialize(MEMORY_FILE_PATH, embeddingModel);
                        memory.Clear();
                        memory.AddDataSources(loadedMemory.DataSources);
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Loaded {memory.DataSources.Count} memory sources from {MEMORY_FILE_PATH}");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Error loading: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "/consolidate":
                    try
                    {
                        if (memory.IsEmpty())
                        {
                            Console.WriteLine("No memories to consolidate.");
                            return;
                        }

                        if (float.TryParse(arg, out float threshold) && threshold > 0)
                            memory.ConsolidationSimilarityThreshold = threshold;

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Consolidating memories (threshold: {memory.ConsolidationSimilarityThreshold:F2})...");
                        Console.ResetColor();

                        var consolidationResult = memory.ConsolidateAsync(chatModel).GetAwaiter().GetResult();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Consolidation complete: merged {consolidationResult.ClustersMerged} cluster(s), " +
                            $"removed {consolidationResult.EntriesRemoved} entries, created {consolidationResult.EntriesCreated} new entries.");
                        Console.WriteLine($"Entries: {consolidationResult.EntryCountBefore} -> {consolidationResult.EntryCountAfter}");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Consolidation error: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "/summarize":
                    try
                    {
                        var chatHistory = executor.ChatHistory;
                        if (chatHistory == null || chatHistory.UserMessageCount < 2)
                        {
                            Console.WriteLine("Not enough conversation to summarize (need at least 2 user messages).");
                            return;
                        }

                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("Summarizing conversation into episodic memory...");
                        Console.ResetColor();

                        var summaryResult = memory.SummarizeConversationAsync(chatHistory, chatModel)
                            .GetAwaiter().GetResult();

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Created {summaryResult.EntriesCreated} episodic memory entry(s) from {summaryResult.MessagePairsSummarized} message pairs:");
                        foreach (var summary in summaryResult.Summaries)
                        {
                            Console.WriteLine($"  - {summary}");
                        }
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Summarization error: {ex.Message}");
                        Console.ResetColor();
                    }
                    break;

                case "/help":
                    ShowHelp();
                    break;

                default:
                    Console.WriteLine($"Unknown command: {cmd}. Type /help for available commands.");
                    break;
            }
        }

        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            if (e.SegmentType == TextSegmentType.UserVisible)
                Console.Write(e.Text);
        }
    }
}
