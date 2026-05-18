using LMKit.Embeddings;
using LMKit.Model;
using System.Globalization;
using System.Text;

namespace embeddings_demo
{
    internal sealed class Chunk
    {
        public string Path { get; init; } = "";
        public int StartLine { get; init; }
        public int EndLine { get; init; }
        public string Text { get; init; } = "";
        public float[] Vector { get; set; } = Array.Empty<float>();
    }

    internal sealed record SearchHit(Chunk Chunk, double Score);

    internal class Program
    {
        static bool _isDownloading;
        static List<Chunk> _chunks = new();
        static Embedder? _embedder;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            WriteHeader();

            LM model = LoadModelInteractive();
            _embedder = new Embedder(model);

            Console.Clear();
            WriteHeader();
            Console.WriteLine($"Embedding dimension: {model.EmbeddingSize}");
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
                    case "1": case "index":
                        IndexFolder();
                        break;
                    case "2": case "demo":
                        LoadBuiltInCorpus();
                        break;
                    case "3": case "search":
                        SearchLoop();
                        break;
                    case "4": case "stats":
                        PrintStats();
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

        static void IndexFolder()
        {
            Console.WriteLine();
            Console.Write("Path to a folder of .txt / .md files: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Folder not found: {dir}");
                Console.ResetColor();
                return;
            }
            Console.Write("Approximate chunk size in characters [600]: ");
            string? cs = Console.ReadLine()?.Trim();
            int chunkSize = int.TryParse(cs, out int n) && n > 100 ? n : 600;

            string[] files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(f => IsTextLike(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                Console.WriteLine("No .txt / .md files found.");
                return;
            }

            var chunks = new List<Chunk>();
            foreach (string f in files) { chunks.AddRange(ChunkFile(f, chunkSize)); }

            Console.WriteLine();
            Console.WriteLine($"  Files     : {files.Length}");
            Console.WriteLine($"  Chunks    : {chunks.Count}");
            Console.WriteLine($"  Embedding ...");
            EmbedChunks(chunks);
            _chunks = chunks;
            Console.WriteLine();
            Console.WriteLine($"Index ready. {chunks.Count} chunk(s) loaded.");
            Console.WriteLine();
        }

        static void LoadBuiltInCorpus()
        {
            string[] corpus =
            {
                "Local LLM inference on a CPU laptop is feasible with quantised models.",
                "Cloud-hosted GPT-4 has the highest quality but costs money per token.",
                "Building a desktop application with .NET MAUI on Windows.",
                "Setting up CUDA drivers for an NVIDIA RTX GPU on Windows 11.",
                "Configuring vector databases like Qdrant or Pinecone for RAG.",
                "Cooking pasta carbonara without cream uses guanciale, egg yolks, and pecorino.",
                "Running quantised Llama models on consumer Windows hardware using llama.cpp.",
                "Fine-tuning a small language model with LoRA on a single GPU.",
                "Indexing a documentation site for semantic search using embeddings.",
                "Recipe: a sourdough starter needs flour, water, and time at room temperature.",
            };

            var chunks = new List<Chunk>(corpus.Length);
            for (int i = 0; i < corpus.Length; i++)
            {
                chunks.Add(new Chunk
                {
                    Path = $"<built-in #{i + 1}>",
                    StartLine = 1,
                    EndLine = 1,
                    Text = corpus[i],
                });
            }
            Console.WriteLine();
            Console.WriteLine($"  Loading {chunks.Count} built-in passages ...");
            EmbedChunks(chunks);
            _chunks = chunks;
            Console.WriteLine();
            Console.WriteLine($"Index ready. {chunks.Count} passage(s) loaded.");
            Console.WriteLine();
        }

        static void SearchLoop()
        {
            if (_chunks.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No index loaded. Pick '1' to index a folder or '2' to load the built-in corpus first.");
                Console.WriteLine();
                return;
            }
            Console.WriteLine();
            Console.Write("Top-K results to show [5]: ");
            int topK = int.TryParse(Console.ReadLine()?.Trim(), out int k) && k > 0 ? k : 5;
            Console.WriteLine("Search mode. Type a query; empty line returns to menu.");
            Console.WriteLine();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("query > ");
                Console.ResetColor();
                string? q = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(q)) { Console.WriteLine(); return; }

                float[] qVec = _embedder!.GetQueryEmbeddings(q);
                var ranked = _chunks
                    .Select(c => new SearchHit(c, Embedder.GetCosineSimilarity(qVec, c.Vector)))
                    .OrderByDescending(h => h.Score)
                    .Take(topK)
                    .ToList();

                Console.WriteLine();
                foreach (var hit in ranked)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  score={hit.Score:F4}  {hit.Chunk.Path}:{hit.Chunk.StartLine}-{hit.Chunk.EndLine}");
                    Console.ResetColor();
                    Console.WriteLine("    " + Truncate(hit.Chunk.Text.Replace("\n", " "), 200));
                }

                Console.Write("\nWrite this result set to a CSV? [y/N]: ");
                string? a = Console.ReadLine()?.Trim();
                if (!string.IsNullOrEmpty(a) && a.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                {
                    string outFile = "search_results.csv";
                    using var w = new StreamWriter(outFile, false, Encoding.UTF8);
                    w.WriteLine("rank,score,path,start_line,end_line,snippet");
                    for (int i = 0; i < ranked.Count; i++)
                    {
                        var h = ranked[i];
                        w.Write(i + 1); w.Write(",");
                        w.Write(h.Score.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                        w.Write(CsvEscape(h.Chunk.Path)); w.Write(",");
                        w.Write(h.Chunk.StartLine); w.Write(",");
                        w.Write(h.Chunk.EndLine); w.Write(",");
                        w.Write(CsvEscape(Truncate(h.Chunk.Text.Replace("\n", " "), 200)));
                        w.WriteLine();
                    }
                    Console.WriteLine($"  CSV: {Path.GetFullPath(outFile)}");
                }
                Console.WriteLine();
            }
        }

        static void PrintStats()
        {
            Console.WriteLine();
            if (_chunks.Count == 0)
            {
                Console.WriteLine("No index loaded.");
                Console.WriteLine();
                return;
            }
            int distinctFiles = _chunks.Select(c => c.Path).Distinct().Count();
            long totalChars = _chunks.Sum(c => (long)c.Text.Length);
            int dim = _chunks[0].Vector.Length;
            Console.WriteLine($"  Chunks      : {_chunks.Count}");
            Console.WriteLine($"  Files       : {distinctFiles}");
            Console.WriteLine($"  Total chars : {totalChars:N0}");
            Console.WriteLine($"  Embed dim   : {dim}");
            Console.WriteLine();
        }

        static void EmbedChunks(List<Chunk> chunks)
        {
            const int batchSize = 32;
            int done = 0;
            for (int i = 0; i < chunks.Count; i += batchSize)
            {
                var batch = chunks.GetRange(i, Math.Min(batchSize, chunks.Count - i));
                string[] texts = batch.Select(c => c.Text).ToArray();
                float[][] vecs = _embedder!.GetEmbeddings(texts);
                for (int j = 0; j < batch.Count; j++)
                {
                    batch[j].Vector = vecs[j];
                }
                done += batch.Count;
                Console.Write($"\r    {done}/{chunks.Count}");
            }
            Console.WriteLine();
        }

        static IEnumerable<Chunk> ChunkFile(string path, int targetSize)
        {
            string text;
            try { text = File.ReadAllText(path, Encoding.UTF8); }
            catch { yield break; }
            string[] lines = text.Replace("\r\n", "\n").Split('\n');

            var sb = new StringBuilder();
            int chunkStart = 1;
            for (int i = 0; i < lines.Length; i++)
            {
                sb.AppendLine(lines[i]);
                bool isBoundary = string.IsNullOrWhiteSpace(lines[i]);
                if ((isBoundary && sb.Length >= targetSize / 2) || sb.Length >= targetSize)
                {
                    string body = sb.ToString().Trim();
                    if (body.Length > 0)
                    {
                        yield return new Chunk
                        {
                            Path = path,
                            StartLine = chunkStart,
                            EndLine = i + 1,
                            Text = body,
                        };
                    }
                    sb.Clear();
                    chunkStart = i + 2;
                }
            }
            if (sb.Length > 0)
            {
                string body = sb.ToString().Trim();
                if (body.Length > 0)
                {
                    yield return new Chunk
                    {
                        Path = path,
                        StartLine = chunkStart,
                        EndLine = lines.Length,
                        Text = body,
                    };
                }
            }
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select an embedding model:");
            Console.WriteLine("  1 - Qwen3 Embedding 0.6B    (small, fast) [Recommended]");
            Console.WriteLine("  2 - Qwen3 Embedding 4B      (higher quality)");
            Console.WriteLine("  3 - Qwen3 Embedding 8B      (best quality)");
            Console.WriteLine("  4 - Embedding Gemma 300M    (very small)");
            Console.Write("\nOr enter a custom model URI / model id\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3-embedding:0.6b",
                "2" => "qwen3-embedding:4b",
                "3" => "qwen3-embedding:8b",
                "4" => "embeddinggemma-300m",
                _ => null,
            };
            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            if (Uri.TryCreate(input.Trim('"'), UriKind.Absolute, out Uri? uri))
            {
                return new LM(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            return LM.LoadFromModelID(input, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static bool IsTextLike(string path) =>
            Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md";

        static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Semantic Search Over Text Corpus            ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Index a folder of .md/.txt files, then run semantic queries with");
            Console.WriteLine("file:line attribution. Built on LM-Kit.NET embeddings.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / index    Index a folder of .md / .txt files");
            Console.WriteLine("  2 / demo     Load the built-in 10-passage corpus");
            Console.WriteLine("  3 / search   Search the loaded index (after 1 or 2)");
            Console.WriteLine("  4 / stats    Show index statistics");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
