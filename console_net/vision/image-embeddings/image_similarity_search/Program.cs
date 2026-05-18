using LMKit.Data;
using LMKit.Embeddings;
using LMKit.Model;
using LMKit.Retrieval;
using System.Globalization;
using System.Text;

namespace image_similarity_search
{
    internal class Program
    {
        static bool _isDownloading;
        static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff" };

        static DataSource? _collection;
        static Embedder? _embedder;
        static readonly Dictionary<string, string> _pathById = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, long> _sizeById = new(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, float[]> _vectorById = new(StringComparer.OrdinalIgnoreCase);

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading nomic-embed-vision ...");
            using LM model = LM.LoadFromModelID(
                "nomic-embed-vision",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            _embedder = new Embedder(model);

            Console.Clear();
            WriteHeader();
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
                        IndexFolder(model);
                        break;
                    case "2": case "duplicates":
                        FindDuplicates();
                        break;
                    case "3": case "search":
                        SearchByQueryImage();
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

        static void IndexFolder(LM model)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of images: ");
            string? inDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Folder not found: {inDir}");
                Console.ResetColor();
                return;
            }
            Console.Write("Output folder for the index [default: <input>/_image_index]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(inDir, "_image_index"); }
            Directory.CreateDirectory(outDir);

            string[] images = Directory.EnumerateFiles(inDir, "*", SearchOption.AllDirectories)
                .Where(f => ImageExt.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (images.Length == 0)
            {
                Console.WriteLine("No images found.");
                return;
            }

            string collectionPath = Path.Combine(outDir, "image_index.ds");
            _collection = DataSource.CreateFileDataSource(
                path: collectionPath,
                identifier: "image-index",
                model: model,
                overwrite: true);
            _pathById.Clear();
            _sizeById.Clear();
            _vectorById.Clear();

            Console.WriteLine();
            Console.WriteLine($"Indexing {images.Length} image(s) ...");
            for (int i = 0; i < images.Length; i++)
            {
                string p = images[i];
                string id = $"img-{i:D6}";
                try
                {
                    float[] v = _embedder!.GetEmbeddings(new Attachment(p));
                    _collection.Upsert(sectionIdentifier: id, vector: v);
                    _pathById[id] = p;
                    _sizeById[id] = new FileInfo(p).Length;
                    _vectorById[id] = v;
                    Console.Write($"\r  {i + 1}/{images.Length}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"Index ready: {Path.GetFullPath(collectionPath)}");
            Console.WriteLine();
        }

        static void FindDuplicates()
        {
            if (_collection == null || _vectorById.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No index loaded. Run option 1 first.");
                Console.WriteLine();
                return;
            }
            Console.WriteLine();
            Console.Write("Similarity threshold (cosine, e.g. 0.92) [0.92]: ");
            double threshold = double.TryParse(Console.ReadLine()?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double t) ? t : 0.92;
            Console.Write("Output folder for the report [default: alongside the index]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = "."; }
            Directory.CreateDirectory(outDir);

            Console.WriteLine();
            Console.WriteLine($"Scanning {_vectorById.Count} image(s) at cosine >= {threshold:F2} ...");

            var visited = new HashSet<string>();
            var clusters = new List<List<(string Id, double Sim)>>();
            foreach ((string id, float[] vec) in _vectorById)
            {
                if (visited.Contains(id)) { continue; }
                var neighbours = VectorSearch.FindMatchingPartitions(
                    dataSources: new[] { _collection },
                    vector: vec);
                var hits = neighbours
                    .Where(n => n.Similarity >= threshold && !visited.Contains(n.SectionIdentifier))
                    .Select(n => (Id: n.SectionIdentifier, Sim: (double)n.Similarity))
                    .ToList();
                if (hits.Count > 1)
                {
                    foreach (var h in hits) { visited.Add(h.Id); }
                    clusters.Add(hits);
                }
            }

            long reclaimable = 0;
            foreach (var cluster in clusters)
            {
                long maxSize = cluster.Max(c => _sizeById.GetValueOrDefault(c.Id, 0));
                reclaimable += cluster.Sum(c => _sizeById.GetValueOrDefault(c.Id, 0)) - maxSize;
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Found {clusters.Count} duplicate cluster(s).");
            Console.WriteLine($"Reclaimable disk space: {reclaimable / 1024.0 / 1024.0:F2} MB");
            Console.ResetColor();
            Console.WriteLine();

            for (int i = 0; i < Math.Min(5, clusters.Count); i++)
            {
                Console.WriteLine($"Cluster {i + 1} ({clusters[i].Count} items):");
                foreach (var c in clusters[i].OrderByDescending(c => c.Sim))
                {
                    string path = _pathById.GetValueOrDefault(c.Id, "(?)");
                    long sz = _sizeById.GetValueOrDefault(c.Id, 0);
                    Console.WriteLine($"  sim={c.Sim:F4}  {sz / 1024,7:N0} KB  {path}");
                }
                Console.WriteLine();
            }
            if (clusters.Count > 5) { Console.WriteLine($"... and {clusters.Count - 5} more cluster(s)."); }

            string md = Path.Combine(outDir, "duplicates_report.md");
            string csv = Path.Combine(outDir, "duplicates.csv");
            WriteDuplicatesMarkdown(clusters, threshold, md);
            WriteDuplicatesCsv(clusters, csv);
            Console.WriteLine();
            Console.WriteLine($"Markdown report : {Path.GetFullPath(md)}");
            Console.WriteLine($"CSV report      : {Path.GetFullPath(csv)}");
            Console.WriteLine();
        }

        static void SearchByQueryImage()
        {
            if (_collection == null || _vectorById.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No index loaded. Run option 1 first.");
                Console.WriteLine();
                return;
            }
            Console.WriteLine();
            Console.Write("Top-K results [10]: ");
            int topK = int.TryParse(Console.ReadLine()?.Trim(), out int k) && k > 0 ? k : 10;

            Console.WriteLine("Search mode. Type a query image path; empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("query > ");
                Console.ResetColor();
                string? path = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(path)) { Console.WriteLine(); return; }
                if (!File.Exists(path))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  not found: {path}");
                    Console.ResetColor();
                    continue;
                }

                float[] qVec = _embedder!.GetEmbeddings(new Attachment(path));
                var hits = VectorSearch.FindMatchingPartitions(
                    dataSources: new[] { _collection },
                    vector: qVec)
                    .Take(topK)
                    .ToList();

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"  Top {hits.Count} match(es):");
                Console.ResetColor();
                foreach (var h in hits)
                {
                    string p = _pathById.GetValueOrDefault(h.SectionIdentifier, h.SectionIdentifier);
                    Console.WriteLine($"    sim={h.Similarity:F4}  {p}");
                }
                Console.WriteLine();
            }
        }

        static void PrintStats()
        {
            Console.WriteLine();
            if (_vectorById.Count == 0)
            {
                Console.WriteLine("No index loaded.");
            }
            else
            {
                Console.WriteLine($"  Images indexed : {_vectorById.Count}");
                Console.WriteLine($"  Total bytes    : {_sizeById.Sum(kv => kv.Value):N0}");
                Console.WriteLine($"  Embed dim      : {_vectorById.First().Value.Length}");
            }
            Console.WriteLine();
        }

        static void WriteDuplicatesMarkdown(
            List<List<(string Id, double Sim)>> clusters,
            double threshold,
            string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("# Near-Duplicate Image Report");
            w.WriteLine();
            w.WriteLine($"- Threshold: cosine ≥ {threshold:F2}");
            w.WriteLine($"- Clusters found: {clusters.Count}");
            w.WriteLine($"- Date: {DateTime.UtcNow:o}");
            w.WriteLine();
            for (int i = 0; i < clusters.Count; i++)
            {
                w.WriteLine($"## Cluster {i + 1} ({clusters[i].Count} items)");
                w.WriteLine();
                w.WriteLine("| Similarity | Size (KB) | Path |");
                w.WriteLine("|---:|---:|---|");
                foreach (var c in clusters[i].OrderByDescending(c => c.Sim))
                {
                    string p = _pathById.GetValueOrDefault(c.Id, "(?)");
                    long sz = _sizeById.GetValueOrDefault(c.Id, 0);
                    w.WriteLine($"| {c.Sim:F4} | {sz / 1024:N0} | `{p}` |");
                }
                w.WriteLine();
            }
        }

        static void WriteDuplicatesCsv(
            List<List<(string Id, double Sim)>> clusters,
            string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("cluster,similarity,size_bytes,path");
            for (int i = 0; i < clusters.Count; i++)
            {
                foreach (var c in clusters[i].OrderByDescending(c => c.Sim))
                {
                    string p = _pathById.GetValueOrDefault(c.Id, "");
                    long sz = _sizeById.GetValueOrDefault(c.Id, 0);
                    w.Write(i + 1); w.Write(",");
                    w.Write(c.Sim.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                    w.Write(sz); w.Write(",");
                    w.Write(CsvEscape(p));
                    w.WriteLine();
                }
            }
        }

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Visual Similarity & Near-Duplicate Index    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Embed a folder of images, cluster near-duplicates by cosine,");
            Console.WriteLine("or search by a query image. Persists to a file-based DataSource.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / index       Embed a folder of images into a persistent index");
            Console.WriteLine("  2 / duplicates  Cluster near-duplicates above a threshold");
            Console.WriteLine("  3 / search      Search the index by a query image");
            Console.WriteLine("  4 / stats       Show index statistics");
            Console.WriteLine("  q / quit        Exit");
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
