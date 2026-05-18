using LMKit.Embeddings;
using LMKit.Model;
using System.Text;

namespace cross_encoder_reranker
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
            using LM embedModel = LM.LoadFromModelID("qwen3-embedding:0.6b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine("Loading bge-m3-reranker ...");
            using LM rerankModel = LM.LoadFromModelID("bge-m3-reranker",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            Embedder embedder = new(embedModel);
            Reranker reranker = new(rerankModel);

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
                    case "1": case "demo":
                        CompareSample(embedder, reranker);
                        break;
                    case "2": case "custom":
                        CompareCustom(embedder, reranker);
                        break;
                    case "3": case "file":
                        CompareFile(embedder, reranker);
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

        static void CompareSample(Embedder embedder, Reranker reranker)
        {
            string[] corpus =
            {
                "Configuration.ThreadCount sets the maximum CPU threads LM-Kit uses for inference.",
                "Bigger models give better quality but require more VRAM and slower CPU inference.",
                "Local LLM inference on a laptop CPU is feasible with quantised models.",
                "Cloud-hosted GPT-4 costs money per token but has the highest quality.",
                "Setting LM.DeviceConfiguration.GpuLayerCount = 0 forces CPU-only execution.",
                "Quantising a model from F16 to Q4_K_M trades a small quality loss for roughly 4x smaller VRAM use.",
                "Cooking pasta carbonara without cream uses guanciale, egg yolks, and pecorino.",
                "Multi-GPU support in LM-Kit uses TensorOverrides for per-tensor placement.",
                "Use IKVCache.HibernateAsync to free RAM during idle periods on long-lived chats.",
            };

            Console.WriteLine();
            Console.WriteLine("Sample corpus: 9 passages about LM-Kit performance tuning (plus one decoy).");
            Console.Write("Query (default: 'How do I tune CPU inference performance in LM-Kit.NET?'): ");
            string query = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) { query = "How do I tune CPU inference performance in LM-Kit.NET?"; }
            CompareAndPrint(embedder, reranker, query, corpus);
        }

        static void CompareCustom(Embedder embedder, Reranker reranker)
        {
            Console.WriteLine();
            Console.Write("Query: ");
            string? query = Console.ReadLine();
            if (string.IsNullOrEmpty(query)) { return; }

            Console.WriteLine("Paste passages one per line, blank line ends:");
            List<string> passages = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{passages.Count + 1}] ");
                Console.ResetColor();
                string? line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) { break; }
                passages.Add(line.Trim());
            }
            if (passages.Count < 2) { Console.WriteLine("Need at least two passages."); return; }
            CompareAndPrint(embedder, reranker, query, passages.ToArray());
        }

        static void CompareFile(Embedder embedder, Reranker reranker)
        {
            Console.WriteLine();
            Console.Write("Path to UTF-8 text file (one passage per line): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }
            string[] passages = File.ReadAllLines(path, Encoding.UTF8).Where(l => !string.IsNullOrWhiteSpace(l)).ToArray();
            if (passages.Length < 2) { Console.WriteLine("Need at least two non-empty lines."); return; }

            Console.Write("Query: ");
            string? query = Console.ReadLine();
            if (string.IsNullOrEmpty(query)) { return; }

            Console.Write("Optional output CSV path (blank to skip): ");
            string? csvPath = Console.ReadLine()?.Trim().Trim('"');
            CompareAndPrint(embedder, reranker, query, passages, csvPath);
        }

        static void CompareAndPrint(Embedder embedder, Reranker reranker, string query, string[] corpus, string? csvPath = null)
        {
            float[] qVec = embedder.GetQueryEmbeddings(query);
            float[][] cVecs = embedder.GetEmbeddings(corpus);
            (string Text, float Score, int Index)[] byCosine = corpus
                .Select((s, i) => (Text: s, Score: Embedder.GetCosineSimilarity(qVec, cVecs[i]), Index: i))
                .OrderByDescending(t => t.Score)
                .ToArray();

            float[] rerankScores = reranker.GetScore(query, corpus);
            (string Text, float Score, int Index)[] byRerank = corpus
                .Select((s, i) => (Text: s, Score: rerankScores[i], Index: i))
                .OrderByDescending(t => t.Score)
                .ToArray();

            Console.WriteLine();
            Console.WriteLine($"Query: \"{query}\"");
            Console.WriteLine();
            Console.WriteLine($"{"#",3}  {"Embedding-only",-50}  {"Score",6}  ||  {"Reranked",-50}  {"Score",7}");
            Console.WriteLine(new string('-', 140));
            for (int i = 0; i < corpus.Length; i++)
            {
                Console.WriteLine(
                    $"{i + 1,3}  {Truncate(byCosine[i].Text, 50),-50}  {byCosine[i].Score,6:F3}  ||  " +
                    $"{Truncate(byRerank[i].Text, 50),-50}  {byRerank[i].Score,7:F3}");
            }
            Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                using StreamWriter csv = new(csvPath, false, new UTF8Encoding(true));
                csv.WriteLine("rank,embedding_index,embedding_score,embedding_text,rerank_index,rerank_score,rerank_text");
                for (int i = 0; i < corpus.Length; i++)
                {
                    csv.Write(i + 1); csv.Write(',');
                    csv.Write(byCosine[i].Index); csv.Write(',');
                    csv.Write($"{byCosine[i].Score:F4}"); csv.Write(',');
                    csv.Write(Csv(byCosine[i].Text)); csv.Write(',');
                    csv.Write(byRerank[i].Index); csv.Write(',');
                    csv.Write($"{byRerank[i].Score:F4}"); csv.Write(',');
                    csv.WriteLine(Csv(byRerank[i].Text));
                }
                Console.WriteLine($"CSV: {csvPath}");
                Console.WriteLine();
            }
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static string Csv(string s)
        {
            if (s == null) { return ""; }
            bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            string body = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{body}\"" : body;
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
            Console.WriteLine("║      Cross-Encoder Reranker Lab                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Side-by-side ranking: bi-encoder cosine vs cross-encoder reranker.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / demo     Built-in 9-passage corpus, type any query");
            Console.WriteLine("  2 / custom   Type your own query and passages");
            Console.WriteLine("  3 / file     Load passages from a UTF-8 text file (CSV export)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
