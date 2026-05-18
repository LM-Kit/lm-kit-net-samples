using LMKit.Model;
using LMKit.Retrieval;
using System.Text;

namespace query_strategies_lab
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading qwen3-embedding:0.6b ...");
            using LM embeddingModel = LM.LoadFromModelID("qwen3-embedding:0.6b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine("Loading qwen3.5:4b (chat) ...");
            using LM chatModel = LM.LoadFromModelID("qwen3.5:4b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            RagEngine engine = new(embeddingModel);
            SeedDefaultCorpus(engine);

            PrintMenu();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "compare":
                        Compare(engine, chatModel);
                        break;
                    case "2": case "import":
                        ImportText(engine);
                        break;
                    case "3": case "importfile":
                        ImportFile(engine);
                        break;
                    case "4": case "reset":
                        engine = new(embeddingModel);
                        SeedDefaultCorpus(engine);
                        Console.WriteLine("Index reset to default corpus.");
                        break;
                    case "q": case "quit": case "exit":
                        return;
                    case "?": case "help": case "menu":
                        PrintMenu();
                        break;
                    default:
                        Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                        break;
                }
            }
        }

        static void SeedDefaultCorpus(RagEngine engine)
        {
            string[] docs =
            {
                "LM-Kit.NET ships precompiled native runtimes for CUDA 12, CUDA 13, Vulkan, Metal, AVX, and AVX2.",
                "Setting Configuration.ThreadCount equal to the number of physical cores improves CPU inference throughput.",
                "The IKVCache.HibernateAsync method serialises a populated KV-cache to disk and rehydrates on next call.",
                "ModelCard.GetPredefinedModelCards lists every model LM-Kit knows how to download.",
                "Configuring the LM.DeviceConfiguration.GpuLayerCount property forces a number of layers onto GPU.",
                "Quantising a model from F16 to Q4_K_M trades a small quality loss for roughly 4x smaller VRAM use.",
                "LoRA adapters are small files that can be applied on top of a base model at inference time.",
            };
            for (int i = 0; i < docs.Length; i++) { engine.ImportText(docs[i], "kb", $"doc{i}"); }
            Console.WriteLine($"Seeded default corpus: {docs.Length} passages.");
        }

        static void ImportText(RagEngine engine)
        {
            Console.WriteLine();
            Console.WriteLine("Paste passages one per line, blank line ends:");
            int n = 0;
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{n + 1}] ");
                Console.ResetColor();
                string? line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) { break; }
                engine.ImportText(line.Trim(), "kb", $"custom{Guid.NewGuid():N}");
                n++;
            }
            Console.WriteLine($"Imported {n} passage(s).");
        }

        static void ImportFile(RagEngine engine)
        {
            Console.WriteLine();
            Console.Write("Path to a UTF-8 text file (one passage per line): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            for (int i = 0; i < lines.Length; i++)
            {
                engine.ImportText(lines[i].Trim(), "kb", $"{Path.GetFileNameWithoutExtension(path)}#{i}");
            }
            Console.WriteLine($"Imported {lines.Length} passage(s) from {Path.GetFileName(path)}.");
        }

        static void Compare(RagEngine engine, LM chatModel)
        {
            Console.WriteLine();
            Console.Write("Question: ");
            string? question = Console.ReadLine();
            if (string.IsNullOrEmpty(question)) { return; }

            Console.Write("Max retrieved partitions (default 3): ");
            int.TryParse(Console.ReadLine(), out int k);
            if (k <= 0) { k = 3; }

            foreach (QueryGenerationMode mode in new[]
            {
                QueryGenerationMode.Original,
                QueryGenerationMode.MultiQuery,
                QueryGenerationMode.HypotheticalAnswer,
            })
            {
                Console.WriteLine();
                Console.WriteLine($"---- {mode} ----");
                using RagChat chat = new(engine, chatModel);
                chat.QueryGenerationMode = mode;
                chat.MaxRetrievedPartitions = k;
                chat.MaximumCompletionTokens = 200;
                if (mode == QueryGenerationMode.MultiQuery) { chat.MultiQueryOptions.QueryVariantCount = 4; }
                if (mode == QueryGenerationMode.HypotheticalAnswer) { chat.HydeOptions.MaxCompletionTokens = 256; }

                try
                {
                    RagQueryResult r = chat.Submit(question);
                    Console.WriteLine("Retrieved partitions:");
                    foreach (PartitionSimilarity p in r.RetrievedPartitions)
                    {
                        Console.WriteLine($"  {p.Similarity:F3}  {p.Payload}");
                    }
                    Console.WriteLine();
                    Console.WriteLine($"Answer: {r.Response.Completion.Trim()}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue) { Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%"); }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      RAG Query Strategies Lab                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Compare Original vs MultiQuery vs HyDE on the same RAG index.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / compare      Run a question across all three query strategies");
            Console.WriteLine("  2 / import       Add typed passages to the index");
            Console.WriteLine("  3 / importfile   Add passages from a UTF-8 text file");
            Console.WriteLine("  4 / reset        Reset to the default 7-passage corpus");
            Console.WriteLine("  q / quit         Exit");
            Console.WriteLine();
        }
    }
}
