using LMKit.Data.Storage;
using LMKit.Integrations.Tesseract;
using LMKit.Model;
using LMKit.Retrieval;
using LMKit.Retrieval.Events;
using System.Text;

namespace chat_with_pdf
{
    internal class Program
    {
        static bool _isDownloading;
        static readonly object _consoleLock = new();

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

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintHeader();

            // Model selection
            PrintSection("Model Selection");
            Console.WriteLine("  0 - Google Gemma 3 4B          (~4 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen-3 8B          (~5.6 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B          (~9 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B       (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B          (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.WriteLine("  *   Or enter a custom model URI");
            Console.WriteLine();

            LM chatModel = PromptModelSelection();

            // Loading models
            Console.WriteLine();
            PrintSection("Loading Models");

            PrintStatus("Chat model loaded", ConsoleColor.Green);

            LM embeddingModel = LM.LoadFromModelID("embeddinggemma-300m");
            PrintStatus("Embedding model loaded", ConsoleColor.Green);

            Console.Clear();
            PrintHeader();

            // Document understanding selection
            PrintSection("Processing Mode");
            Console.WriteLine("  0 - Standard text extraction (faster)");
            Console.WriteLine("  1 - Vision-based document understanding (better for complex layouts)");
            Console.WriteLine();

            bool useDocumentUnderstanding = PromptProcessingMode();

            string cacheDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "LMKit", "ChatWithPDF", "Cache");

            IVectorStore vectorStore = new FileSystemVectorStore(cacheDirectory);

            // Initialize PdfChat
            PdfChat chat = new PdfChat(
                chatModel,
                embeddingModel,
                vectorStore);

            //chat.IncludePageRenderingsInContext = true; // uncomment to snapshot document pages in the context

            if (useDocumentUnderstanding)
            {
                chat.PageProcessingMode = PageProcessingMode.DocumentUnderstanding;
                chat.DocumentVisionParser = new LMKit.Extraction.Ocr.VlmOcr(LM.LoadFromModelID("lightonocr-2:1b"));
                PrintStatus("Document understanding enabled", ConsoleColor.Cyan);
            }
            else
            {
                chat.OcrEngine = new TesseractOcr()
                {
                    EnableLanguageDetection = true,
                    VisionModel = chatModel
                };
                PrintStatus("Standard extraction with OCR fallback", ConsoleColor.Cyan);
            }

            // Subscribe to all events
            chat.DocumentImportProgress += Chat_DocumentImportProgress;
            chat.CacheAccessed += Chat_CacheAccessed;
            chat.PassageRetrievalCompleted += Chat_PassageRetrievalCompleted;
            chat.ResponseGenerationStarted += Chat_ResponseGenerationStarted;
            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            Console.WriteLine();
            PrintCommands();

            string mode = "load_document";
            string prompt = "";

            while (true)
            {
                if (mode == "load_document")
                {
                    if (!TryLoadDocument(chat))
                    {
                        continue;
                    }

                    mode = "chat";
                    PrintChatReady();
                }

                if (mode != "regenerate")
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("  Ask a question: ");
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

                // Generate response
                DocumentQueryResult result;

                try
                {
                    if (mode == "regenerate")
                    {
                        result = chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                        mode = "chat";
                    }
                    else
                    {
                        result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    }

                    Console.WriteLine();
                    PrintResponseStats(result);
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

        #region Event Handlers

        private static void Chat_DocumentImportProgress(object? sender, DocumentImportProgressEventArgs e)
        {
            lock (_consoleLock)
            {
                switch (e.Phase)
                {
                    case DocumentImportPhase.PageProcessingStarted:
                        string strategy = e.PageStrategy == PageProcessingMode.DocumentUnderstanding
                            ? "vision"
                            : "text";
                        int percent = (int)((e.PageIndex + 1) / (float)e.TotalPages * 100);
                        string progressBar = BuildProgressBar(percent, 20);

                        Console.Write($"\r  {progressBar} Page {e.PageIndex + 1}/{e.TotalPages} [{strategy}]".PadRight(60));
                        break;

                    case DocumentImportPhase.PageProcessingCompleted:
                        if (e.PageIndex == e.TotalPages - 1)
                        {
                            ClearCurrentLine();
                            PrintStatus($"  Processed {e.TotalPages} page(s)", ConsoleColor.DarkGray);
                        }
                        break;

                    case DocumentImportPhase.EmbeddingStarted:
                        Console.Write($"\r  Generating embeddings for {e.SectionCount} section(s)...".PadRight(60));
                        break;

                    case DocumentImportPhase.EmbeddingCompleted:
                        ClearCurrentLine();
                        PrintStatus($"  Indexed {e.SectionCount} section(s)", ConsoleColor.DarkGray);
                        break;
                }
            }
        }

        private static void Chat_CacheAccessed(object? sender, CacheAccessedEventArgs e)
        {
            lock (_consoleLock)
            {
                if (e.IsHit)
                {
                    PrintStatus("  Cache hit: loaded pre-indexed data", ConsoleColor.DarkGreen);
                }
                else
                {
                    PrintStatus("  Cache miss: indexing document...", ConsoleColor.DarkGray);
                }
            }
        }

        private static void Chat_PassageRetrievalCompleted(object? sender, PassageRetrievalCompletedEventArgs e)
        {
            lock (_consoleLock)
            {
                Console.WriteLine();

                if (e.RetrievedCount == 0)
                {
                    PrintStatus("  No relevant passages found", ConsoleColor.DarkYellow);
                }
                else
                {
                    PrintStatus($"  Retrieved {e.RetrievedCount} passage(s) in {e.Elapsed.TotalMilliseconds:F0}ms", ConsoleColor.DarkCyan);

                    // Group references by document for cleaner display
                    var byDocument = e.References
                        .GroupBy(r => r.Name)
                        .ToList();

                    foreach (var group in byDocument)
                    {
                        var pages = group.Select(r => r.PageNumber).Distinct().OrderBy(p => p).ToList();
                        string pageList = pages.Count <= 5
                            ? string.Join(", ", pages)
                            : $"{string.Join(", ", pages.Take(4))}... +{pages.Count - 4} more";

                        PrintStatus($"      {group.Key}: page(s) {pageList}", ConsoleColor.DarkGray);
                    }
                }
            }
        }

        private static void Chat_ResponseGenerationStarted(object? sender, ResponseGenerationStartedEventArgs e)
        {
            lock (_consoleLock)
            {
                Console.WriteLine();

                if (e.UsesFullContext)
                {
                    PrintStatus("  Generating response using full document context...", ConsoleColor.DarkCyan);
                }
                else
                {
                    PrintStatus($"  Generating response from {e.PassageCount} passage(s)...", ConsoleColor.DarkCyan);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("  Assistant: ");
                Console.ResetColor();
            }
        }

        private static void Chat_AfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.Write(e.Text);
            Console.ResetColor();
        }

        #endregion

        #region Document Loading

        private static bool TryLoadDocument(PdfChat chat)
        {
            while (true)
            {
                Console.WriteLine();
                Console.Write("  Enter PDF path: ");
                string? pathInput = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(pathInput))
                {
                    return chat.HasDocuments;
                }

                string path = pathInput.Trim().Trim('"');

                if (!File.Exists(path))
                {
                    PrintStatus("  File not found", ConsoleColor.Red);
                    continue;
                }

                try
                {
                    Console.WriteLine();
                    var result = chat.LoadDocument(path);

                    string modeText = result.IndexingMode == DocumentIndexingResult.DocumentIndexingMode.FullDocument
                        ? "full context"
                        : "passage retrieval";

                    Console.WriteLine();
                    PrintStatus($"Loaded: {result.Name}", ConsoleColor.Green);
                    PrintStatus($"    Pages: {result.PageCount}", ConsoleColor.DarkGray);
                    PrintStatus($"    Tokens: {result.TokenCount:N0}", ConsoleColor.DarkGray);
                    PrintStatus($"    Mode: {modeText}", ConsoleColor.DarkGray);

                    if (result.ExceededTokenBudget)
                    {
                        PrintStatus("    Exceeded token budget, using passage retrieval", ConsoleColor.DarkYellow);
                    }

                    Console.WriteLine();
                    Console.Write("  Load another document? (y/N): ");
                    string another = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();

                    if (another != "y" && another != "yes")
                    {
                        return true;
                    }
                }
                catch (Exception e)
                {
                    PrintStatus($"  Error: {e.Message}", ConsoleColor.Red);
                }
            }
        }

        #endregion

        #region Command Handling

        private static bool TryHandleCommand(string prompt, PdfChat chat, ref string mode)
        {
            string cmd = prompt.Trim().ToLowerInvariant();

            switch (cmd)
            {
                case "/reset":
                    chat.ClearDocuments();
                    Console.WriteLine();
                    PrintStatus("All documents removed and chat history cleared", ConsoleColor.Yellow);
                    PrintStatus("  You will be prompted to load new documents.", ConsoleColor.DarkGray);
                    mode = "load_document";
                    return true;

                case "/restart":
                    chat.ClearHistory();
                    Console.WriteLine();
                    PrintStatus("Chat history cleared", ConsoleColor.Yellow);
                    PrintStatus("  Documents are still loaded. Start a fresh conversation.", ConsoleColor.DarkGray);
                    return true;

                case "/add":
                    chat.ClearHistory();
                    Console.WriteLine();
                    PrintStatus("Chat history cleared", ConsoleColor.Yellow);
                    PrintStatus("  Add more documents to the existing collection.", ConsoleColor.DarkGray);
                    if (!TryLoadDocument(chat))
                    {
                        PrintStatus("  No document added, continuing with existing documents.", ConsoleColor.DarkGray);
                    }
                    PrintChatReady();
                    return true;

                case "/regenerate":
                    mode = "regenerate";
                    return false;

                case "/help":
                    Console.WriteLine();
                    PrintCommandsDetailed();
                    return true;

                case "/status":
                    Console.WriteLine();
                    PrintStatusDetailed(chat);
                    return true;

                default:
                    return false;
            }
        }

        #endregion

        #region UI Helpers

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine("  ===================================================");
            Console.WriteLine("              LM-Kit PDF Chat Assistant               ");
            Console.WriteLine("  ===================================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintSection(string title)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  -- {title} --");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintDivider()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  " + new string('-', 59));
            Console.ResetColor();
        }

        private static void PrintStatus(string message, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine($"  {message}");
            Console.ResetColor();
        }

        private static void PrintCommands()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Type /help to see available commands.");
            Console.ResetColor();
        }

        private static void PrintChatReady()
        {
            Console.WriteLine();
            PrintDivider();
            PrintStatus("Chat is ready.", ConsoleColor.Green);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  Type a question and press Enter.");
            Console.WriteLine("  Examples:");
            Console.WriteLine("    - Summarize the document");
            Console.WriteLine("    - What are the key requirements?");
            Console.WriteLine("    - Explain page 3 in simple terms");
            Console.WriteLine("  Commands: /help, /status, /regenerate (empty line exits)");
            Console.ResetColor();

            PrintDivider();
            Console.WriteLine();
        }

        private static void PrintCommandsDetailed()
        {
            PrintSection("Available Commands");

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /help");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Show this help message.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /status");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Display detailed information about loaded documents,");
            Console.WriteLine("      token usage, and current configuration.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /add");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Add more PDF documents to the current collection.");
            Console.WriteLine("      Clears chat history but keeps existing documents.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /restart");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Start a new conversation with the same documents.");
            Console.WriteLine("      Clears chat history but keeps all loaded documents.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /reset");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Remove all documents and clear chat history.");
            Console.WriteLine("      You will be prompted to load new documents.");
            Console.WriteLine();

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  /regenerate");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("      Generate a new response to your last question.");
            Console.WriteLine("      Useful for getting alternative answers.");
            Console.WriteLine();

            Console.ResetColor();
        }

        private static void PrintStatusDetailed(PdfChat chat)
        {
            PrintSection("Current Status");

            // Documents
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Documents");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      Total loaded:      {chat.DocumentCount}");
            Console.WriteLine($"      Full context:      {chat.FullDocumentCount}");
            Console.WriteLine($"      Passage retrieval: {chat.PassageRetrievalDocumentCount}");
            Console.WriteLine();

            // Token budget
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Token Budget");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            int usedPercent = chat.FullDocumentTokenBudget > 0
                ? (int)(chat.UsedDocumentTokens / (float)chat.FullDocumentTokenBudget * 100)
                : 0;
            Console.WriteLine($"      Used:      {chat.UsedDocumentTokens:N0} / {chat.FullDocumentTokenBudget:N0} ({usedPercent}%)");
            Console.WriteLine($"      Remaining: {chat.RemainingDocumentTokenBudget:N0}");
            Console.WriteLine();

            // Configuration
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Configuration");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      Processing mode:      {chat.PageProcessingMode}");
            Console.WriteLine($"      Max passages/query:   {chat.MaxRetrievedPassages}");
            Console.WriteLine($"      Min relevance score:  {chat.MinRelevanceScore:F2}");
            Console.WriteLine($"      Context size:         {chat.ContextSize:N0} tokens");
            Console.WriteLine();

            // Conversation
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("  Conversation");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"      Messages: {chat.ChatHistory.MessageCount}");
            Console.WriteLine();

            Console.ResetColor();
        }

        private static void PrintResponseStats(DocumentQueryResult result)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine(
                $"  [{result.Response.GeneratedTokens.Count} tokens | " +
                $"{result.Response.TokenGenerationRate:F1} tok/s | " +
                $"ctx: {result.Response.ContextTokens.Count}/{result.Response.ContextSize}]");
            Console.ResetColor();
        }

        private static string BuildProgressBar(int percent, int width)
        {
            int filled = (int)(percent / 100f * width);
            int empty = width - filled;
            return $"[{new string('#', filled)}{new string('.', empty)}] {percent,3}%";
        }

        private static void ClearCurrentLine()
        {
            Console.Write("\r" + new string(' ', 70) + "\r");
        }

        private static LM PromptModelSelection()
        {
            while (true)
            {
                Console.Write("  Select: ");
                string input = Console.ReadLine()?.Trim() ?? "";

                string? modelId = input switch
                {
                    "0" => "gemma3:4b",
                    "1" => "qwen3:8b",
                    "2" => "gemma3:12b",
                    "3" => "phi4",
                    "4" => "gptoss:20b",
                    "5" => "glm4.7-flash",
                    _ => null
                };

                if (modelId != null)
                {
                    return LM.LoadFromModelID(
                        modelId,
                        downloadingProgress: OnDownloadProgress,
                        loadingProgress: OnLoadProgress);
                }

                // Otherwise, try to parse the input as a custom URI
                if (!string.IsNullOrWhiteSpace(input))
                {
                    string trimmedInput = input.Trim('"');

                    if (Uri.TryCreate(trimmedInput, UriKind.Absolute, out Uri? customUri))
                    {
                        return new LM(
                            customUri,
                            downloadingProgress: OnDownloadProgress,
                            loadingProgress: OnLoadProgress);
                    }
                }

                PrintStatus("  Invalid selection. Enter 0-5 or a valid model URI.", ConsoleColor.Red);
                Console.WriteLine();
            }
        }

        private static bool PromptProcessingMode()
        {
            while (true)
            {
                Console.Write("  Select: ");
                string? input = Console.ReadLine()?.Trim();

                if (input == "0")
                {
                    return false;
                }

                if (input == "1")
                {
                    return true;
                }

                PrintStatus("  Invalid selection. Please enter 0 or 1.", ConsoleColor.Red);
                Console.WriteLine();
            }
        }

        #endregion
    }
}
