using LMKit.Agents;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Text;

namespace persistent_memory_assistant
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

        static readonly string EMBEDDING_MODEL_PATH = @"https://huggingface.co/lm-kit/bge-m3-gguf/resolve/main/bge-m3-f16.gguf";
        static readonly string MEMORY_FILE_PATH = "./agent_memory.bin";

        static bool _isDownloading;
        static string _currentDownload = "";

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            string modelName = path.Contains("bge") ? "embedding model" : "chat model";
            if (_currentDownload != modelName)
            {
                _currentDownload = modelName;
                Console.WriteLine();
            }

            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading {modelName} {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading {modelName} {bytesRead} bytes");
            }
            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.WriteLine();
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
            Console.WriteLine("=== Persistent Memory Assistant Demo ===\n");
            Console.WriteLine("This demo showcases agent memory for context across conversations.");
            Console.WriteLine("The assistant remembers information you share and recalls it later.\n");

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

            Console.WriteLine("\nLoading models (chat + embedding for memory)...\n");

            // Load chat model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.WriteLine();

            // Load embedding model for memory
            Uri embeddingUri = new(EMBEDDING_MODEL_PATH);
            var embeddingModel = new LM(embeddingUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.WriteLine("=== Persistent Memory Assistant ===\n");

            // Create agent memory
            var memory = new AgentMemory(embeddingModel);

            // Try to load existing memories
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
                }
            }

            // Create agent with memory
            var agent = Agent.CreateBuilder(model)
                .WithPersona(@"You are a helpful personal assistant with persistent memory.
You remember information users share with you and use it to provide personalized assistance.

When users share personal information (name, preferences, projects, etc.):
- Acknowledge and confirm what you've learned
- Use this information naturally in future responses

When answering questions:
- Check if you have relevant memories that could help
- Reference past conversations when appropriate
- Be conversational and personable

If asked about what you remember, summarize the key facts you know about the user.")
                .WithPlanning(PlanningStrategy.None)
                .WithMemory(memory)
                .Build();

            // Create executor and attach event handler for streaming output
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
                {
                    continue;
                }

                // Handle commands
                if (userInput.StartsWith("/"))
                {
                    HandleCommand(userInput, memory, embeddingModel);
                    continue;
                }

                if (userInput.Equals("quit", StringComparison.OrdinalIgnoreCase))
                {
                    // Auto-save on exit
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

                    // Get response
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nAssistant: ");
                    Console.ResetColor();

                    var result = executor.Execute(
                        agent,
                        userInput,
                        cancellationToken: cts.Token);

                    Console.WriteLine();

                    // Extract and store new information from the conversation
                    ExtractAndStoreMemories(userInput, result.Content ?? "", memory);
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

            Console.WriteLine("\nThank you for using Persistent Memory Assistant. Press any key to exit.");
            Console.ReadKey();
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  /remember <info> - Store information in memory");
            Console.WriteLine("  /memories        - List all stored memory sources");
            Console.WriteLine("  /clear           - Clear all memories");
            Console.WriteLine("  /save            - Save memories to disk");
            Console.WriteLine("  /load            - Load memories from disk");
            Console.WriteLine("  /help            - Show this help");
            Console.WriteLine("  quit             - Exit (auto-saves)\n");
            Console.WriteLine("Chat naturally - the assistant will remember what you share!");
        }

        private static void HandleCommand(string command, AgentMemory memory, LM embeddingModel)
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
                    Console.WriteLine($"Memory sources ({dataSources.Count}):");
                    Console.ResetColor();
                    foreach (var ds in dataSources)
                    {
                        var memType = AgentMemory.GetMemoryType(ds);
                        Console.WriteLine($"  [{memType}] {ds.Identifier} - {ds.Sections.Count()} section(s)");
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
                        memory = AgentMemory.Deserialize(MEMORY_FILE_PATH, embeddingModel);
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

                case "/help":
                    ShowHelp();
                    break;

                default:
                    Console.WriteLine($"Unknown command: {cmd}. Type /help for available commands.");
                    break;
            }
        }

        /// <summary>
        /// Simple heuristic to extract facts from conversation and store them.
        /// In production, you might use the LLM itself to extract entities/facts.
        /// </summary>
        private static void ExtractAndStoreMemories(string userMessage, string assistantResponse, AgentMemory memory)
        {
            // Simple patterns to detect information worth remembering
            var patterns = new[]
            {
                ("my name is ", MemoryType.Semantic),
                ("i work ", MemoryType.Semantic),
                ("i am a ", MemoryType.Semantic),
                ("i live in ", MemoryType.Semantic),
                ("i prefer ", MemoryType.Procedural),
                ("i like ", MemoryType.Semantic),
                ("my favorite ", MemoryType.Semantic),
                ("i'm working on ", MemoryType.Episodic),
                ("my project ", MemoryType.Episodic),
                ("yesterday ", MemoryType.Episodic),
                ("today ", MemoryType.Episodic),
                ("remember that ", MemoryType.Semantic),
            };

            var messageLower = userMessage.ToLowerInvariant();

            foreach (var (pattern, memoryType) in patterns)
            {
                if (messageLower.Contains(pattern))
                {
                    // Store a condensed version
                    var condensed = userMessage.Length > 200 ? userMessage.Substring(0, 200) + "..." : userMessage;
                    string dataSourceId = memoryType.ToString().ToLower() + "_memories";
                    string sectionId = $"conversation_{DateTime.Now.Ticks}";
                    memory.SaveInformation(dataSourceId, $"User said: {condensed}", sectionId, memoryType);

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"[Stored new {memoryType.ToString().ToLower()} memory]");
                    Console.ResetColor();
                    break; // Only store once per message
                }
            }
        }

        /// <summary>
        /// Event handler to display agent output in real-time.
        /// </summary>
        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            if (e.SegmentType == TextSegmentType.UserVisible)
            {
                Console.Write(e.Text);
            }
        }
    }
}
