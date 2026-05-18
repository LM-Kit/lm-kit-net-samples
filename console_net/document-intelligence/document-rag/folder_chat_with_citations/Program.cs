using LMKit.Model;
using LMKit.Retrieval;
using System.Text;

namespace document_rag
{
    internal class Program
    {
        private static bool _isDownloading;

        static int Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Document RAG Demo ===\n");

            if (args.Length == 0)
            {
                Console.WriteLine("Pass a folder path containing your documents.");
                Console.WriteLine("Example: dotnet run -- C:\\docs\\policies");
                return 1;
            }
            string dir = args[0];
            if (!Directory.Exists(dir))
            {
                Console.WriteLine($"Directory not found: {dir}");
                return 1;
            }

            Console.WriteLine("Loading embedding model qwen3-embedding:0.6b ...");
            using LM embed = LM.LoadFromModelID(
                "qwen3-embedding:0.6b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine("Loading chat model qwen3.5:4b ...");
            using LM chatModel = LM.LoadFromModelID(
                "qwen3.5:4b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            RagEngine engine = new(embed);

            string[] exts = [".pdf", ".txt", ".md", ".docx", ".html", ".htm", ".eml"];
            string[] files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
                .Where(p => exts.Contains(Path.GetExtension(p), StringComparer.OrdinalIgnoreCase))
                .ToArray();

            Console.WriteLine($"Indexing {files.Length} document(s)...");
            int ok = 0;
            foreach (string f in files)
            {
                try
                {
                    engine.ImportTextFromFile(f, Encoding.UTF8, "kb", Path.GetFileName(f));
                    ok++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  [skipped] {Path.GetFileName(f)} : {ex.Message}");
                }
            }
            Console.WriteLine($"Indexed {ok} / {files.Length} document(s).");
            Console.WriteLine();

            using RagChat chat = new(engine, chatModel)
            {
                MaxRetrievedPartitions = 4,
                MaximumCompletionTokens = 500,
            };
            chat.QueryGenerationMode = QueryGenerationMode.Contextual;

            Console.WriteLine("Ask a question (blank line to exit).");
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write("\n> ");
                Console.ResetColor();
                string q = Console.ReadLine() ?? "";
                if (string.IsNullOrWhiteSpace(q)) { break; }

                RagQueryResult r = chat.Submit(q);
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(r.Response.Completion.Trim());
                Console.ResetColor();
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Citations ({r.RetrievedPartitions.Count}):");
                foreach (PartitionSimilarity p in r.RetrievedPartitions)
                {
                    Console.WriteLine($"  - {p.SectionIdentifier} ({p.Similarity:F2})");
                }
                Console.ResetColor();
            }

            return 0;
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
