using LMKit.Data;
using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace named_entity_recognition
{
    internal sealed record Occurrence(
        string DocumentPath, string Label, string Value, string Normalised, double Confidence);

    internal sealed record RegistryRow(
        string Label, string Normalised, string Representative,
        int Occurrences, int DistinctDocuments, double MaxConfidence,
        List<string> DocumentPaths);

    internal class Program
    {
        static bool _isDownloading;

        static readonly HashSet<string> SupportedExt = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".docx", ".txt", ".md", ".eml", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp" };

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            WriteHeader();

            LM model = LoadModelInteractive();
            NamedEntityRecognition engine = new(model);

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
                    case "1": case "live":
                        RunLiveMode(engine);
                        break;
                    case "2": case "file":
                        RunSingleFileMode(engine);
                        break;
                    case "3": case "folder":
                        RunFolderMode(engine);
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

        static void RunLiveMode(NamedEntityRecognition engine)
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Paste a paragraph; empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("text > ");
                Console.ResetColor();
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine(); return; }

                Stopwatch sw = Stopwatch.StartNew();
                var entities = engine.Recognize(text);
                sw.Stop();

                Console.WriteLine($"  {entities.Count} entit(y/ies) in {sw.Elapsed.TotalSeconds:F1}s:");
                foreach (var e in entities.OrderByDescending(e => e.Confidence))
                {
                    Console.WriteLine($"    {e.EntityDefinition.Label,-18}  {e.Value,-40} ({e.Confidence:P0})");
                }
                Console.WriteLine();
            }
        }

        static void RunSingleFileMode(NamedEntityRecognition engine)
        {
            Console.WriteLine();
            Console.Write("Path to a document (.pdf .docx .txt .md .eml .png .jpg ...): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { return; }
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {path}");
                Console.ResetColor();
                return;
            }

            try
            {
                using var att = new Attachment(path);
                Stopwatch sw = Stopwatch.StartNew();
                var entities = engine.Recognize(att);
                sw.Stop();
                Console.WriteLine();
                Console.WriteLine($"{entities.Count} entit(y/ies) in {sw.Elapsed.TotalSeconds:F1}s:");
                foreach (var e in entities.OrderByDescending(e => e.Confidence))
                {
                    Console.WriteLine($"  {e.EntityDefinition.Label,-18}  {e.Value,-40} ({e.Confidence:P0})");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static void RunFolderMode(NamedEntityRecognition engine)
        {
            Console.WriteLine();
            Console.Write("Path to a folder containing documents: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Folder not found: {dir}");
                Console.ResetColor();
                return;
            }
            Console.Write("Recurse into sub-folders? [y/N]: ");
            bool recursive = (Console.ReadLine()?.Trim() ?? "").StartsWith("y", StringComparison.OrdinalIgnoreCase);
            Console.Write("Minimum confidence threshold [0.0]: ");
            string? mcStr = Console.ReadLine()?.Trim();
            double minConfidence = double.TryParse(mcStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double mc) ? mc : 0.0;
            Console.Write("Output folder for the registry [default: <input>/_registry]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(dir, "_registry"); }
            Directory.CreateDirectory(outDir);

            string[] files = Directory.EnumerateFiles(
                    dir, "*", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly)
                .Where(f => SupportedExt.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                Console.WriteLine("No supported documents in the folder.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Recognising entities in {files.Length} document(s) ...");
            Console.WriteLine();
            var occurrences = new List<Occurrence>();
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                Console.WriteLine($"  [{i + 1}/{files.Length}] {Path.GetFileName(path)}");
                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    using var att = new Attachment(path);
                    var detected = engine.Recognize(att);
                    sw.Stop();
                    int kept = 0;
                    foreach (var e in detected)
                    {
                        if (e.Confidence < minConfidence) { continue; }
                        kept++;
                        occurrences.Add(new Occurrence(
                            path, e.EntityDefinition.Label, e.Value, Normalise(e.Value), e.Confidence));
                    }
                    Console.WriteLine($"        {detected.Count} found, {kept} kept ({sw.Elapsed.TotalSeconds:F1}s)");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"        [error] {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();

            if (occurrences.Count == 0)
            {
                Console.WriteLine("No entities passed the confidence threshold.");
                return;
            }

            var registry = BuildRegistry(occurrences);
            PrintTopEntitiesByLabel(registry);

            string regCsv = Path.Combine(outDir, "entities_registry.csv");
            string occCsv = Path.Combine(outDir, "entities_occurrences.csv");
            WriteRegistryCsv(registry, regCsv);
            WriteOccurrencesCsv(occurrences, occCsv);
            Console.WriteLine();
            Console.WriteLine($"Registry CSV     : {Path.GetFullPath(regCsv)}");
            Console.WriteLine($"Occurrences CSV  : {Path.GetFullPath(occCsv)}");
            Console.WriteLine();
        }

        static List<RegistryRow> BuildRegistry(List<Occurrence> occurrences) =>
            occurrences
                .GroupBy(o => (o.Label, o.Normalised))
                .Select(g =>
                {
                    var rep = g.OrderByDescending(o => o.Confidence).First();
                    return new RegistryRow(
                        Label: g.Key.Label,
                        Normalised: g.Key.Normalised,
                        Representative: rep.Value,
                        Occurrences: g.Count(),
                        DistinctDocuments: g.Select(o => o.DocumentPath).Distinct().Count(),
                        MaxConfidence: g.Max(o => o.Confidence),
                        DocumentPaths: g.Select(o => o.DocumentPath).Distinct().ToList());
                })
                .OrderByDescending(r => r.DistinctDocuments)
                .ThenByDescending(r => r.Occurrences)
                .ToList();

        static void PrintTopEntitiesByLabel(List<RegistryRow> registry)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Top entities by label (most cross-document, then most-cited):");
            Console.ResetColor();
            const int TopPerLabel = 5;
            foreach (var labelGroup in registry.GroupBy(r => r.Label).OrderBy(g => g.Key))
            {
                Console.WriteLine($"\n  [{labelGroup.Key}]");
                foreach (var row in labelGroup.Take(TopPerLabel))
                {
                    Console.WriteLine(
                        $"    {Truncate(row.Representative, 40),-40}  " +
                        $"docs={row.DistinctDocuments,3}  occ={row.Occurrences,4}  " +
                        $"max-conf={row.MaxConfidence:P0}");
                }
            }
        }

        static string Normalise(string s)
        {
            string lower = s.Trim().ToLowerInvariant();
            while (lower.Length > 0 && (char.IsPunctuation(lower[^1]) || lower[^1] == '.' || lower[^1] == ','))
            {
                lower = lower.Substring(0, lower.Length - 1).TrimEnd();
            }
            var sb = new StringBuilder(lower.Length);
            bool ws = false;
            foreach (char c in lower)
            {
                if (char.IsWhiteSpace(c))
                {
                    if (!ws) { sb.Append(' '); ws = true; }
                }
                else { sb.Append(c); ws = false; }
            }
            return sb.ToString();
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 2B          (~2 GB VRAM) [Fast]");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 4B          (~3.5 GB VRAM)");
            Console.WriteLine("  3 - Alibaba Qwen 3.5 9B          (~7 GB VRAM) [Recommended]");
            Console.WriteLine("  4 - Google Gemma 4 E4B           (~6 GB VRAM)");
            Console.WriteLine("  5 - Alibaba Qwen 3.6 27B         (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.6 35B-A3B     (~22 GB VRAM)");
            Console.WriteLine("  7 - Google Gemma 4 26B-A4B       (~18 GB VRAM)");
            Console.Write("\nOr enter a custom model URI\n> ");

            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3.5:2b",
                "2" => "qwen3.5:4b",
                "3" => "qwen3.5:9b",
                "4" => "gemma4:e4b",
                "5" => "qwen3.6:27b",
                "6" => "qwen3.6:35b-a3b",
                "7" => "gemma4:26b-a4b",
                _ => null,
            };
            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static void WriteRegistryCsv(List<RegistryRow> registry, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("label,normalised,representative,occurrences,distinct_documents,max_confidence,documents");
            foreach (var r in registry)
            {
                w.Write(CsvEscape(r.Label)); w.Write(",");
                w.Write(CsvEscape(r.Normalised)); w.Write(",");
                w.Write(CsvEscape(r.Representative)); w.Write(",");
                w.Write(r.Occurrences); w.Write(",");
                w.Write(r.DistinctDocuments); w.Write(",");
                w.Write(r.MaxConfidence.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(CsvEscape(string.Join(";", r.DocumentPaths.Select(Path.GetFileName))));
                w.WriteLine();
            }
        }

        static void WriteOccurrencesCsv(List<Occurrence> occurrences, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("document,label,value,normalised,confidence");
            foreach (var o in occurrences)
            {
                w.Write(CsvEscape(o.DocumentPath)); w.Write(",");
                w.Write(CsvEscape(o.Label)); w.Write(",");
                w.Write(CsvEscape(o.Value)); w.Write(",");
                w.Write(CsvEscape(o.Normalised)); w.Write(",");
                w.Write(o.Confidence.ToString("F4", CultureInfo.InvariantCulture));
                w.WriteLine();
            }
        }

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Multi-Document Entity Registry              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Cross-document NER. Recognise entities once, dedupe across files,");
            Console.WriteLine("rank by recurrence, export to CSV for compliance / discovery.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Recognise entities in pasted text");
            Console.WriteLine("  2 / file     Recognise entities in a single document");
            Console.WriteLine("  3 / folder   Build a cross-document entity registry from a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
