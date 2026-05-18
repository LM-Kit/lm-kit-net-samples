using LMKit.Data;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;
using System.Text.Json;

namespace image_labeling
{
    internal class Program
    {
        static bool _isDownloading;
        static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff" };

        static Dictionary<string, List<string>> _perImage = new(StringComparer.OrdinalIgnoreCase);
        static Dictionary<string, List<string>> _inverted = new(StringComparer.OrdinalIgnoreCase);
        static LM? _model;
        static Grammar? _jsonArrayGrammar;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            _model = LoadModelInteractive();
            if (!_model.HasVision)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nERROR: model does not support vision.");
                Console.ResetColor();
                return;
            }
            _jsonArrayGrammar = new Grammar(Grammar.PredefinedGrammar.JsonStringArray);

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
                        RunLiveMode();
                        break;
                    case "2": case "index":
                        IndexFolder();
                        break;
                    case "3": case "lookup":
                        LookupLoop();
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

        static void RunLiveMode()
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Type an image path; empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("image > ");
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
                List<string> tags = TagImage(path);
                Console.WriteLine($"  -> {string.Join(", ", tags)}");
                Console.WriteLine();
            }
        }

        static void IndexFolder()
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
            Console.Write("Output folder for the index [default: <input>/_tags]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(inDir, "_tags"); }
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

            Console.WriteLine();
            Console.WriteLine($"Tagging {images.Length} image(s) ...");
            Console.WriteLine();
            _perImage = new(StringComparer.OrdinalIgnoreCase);
            _inverted = new(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < images.Length; i++)
            {
                string p = images[i];
                try
                {
                    List<string> tags = TagImage(p);
                    _perImage[p] = tags;
                    foreach (string t in tags)
                    {
                        if (!_inverted.TryGetValue(t, out var list))
                        {
                            list = new List<string>();
                            _inverted[t] = list;
                        }
                        list.Add(p);
                    }
                    Console.WriteLine($"  [{i + 1,3}/{images.Length}] {Path.GetFileName(p),-32} -> {string.Join(", ", tags)}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }

            string indexPath = Path.Combine(outDir, "tags_index.json");
            string invertedPath = Path.Combine(outDir, "tags_inverted.csv");
            WriteIndexJson(_perImage, indexPath);
            WriteInvertedCsv(_inverted, invertedPath);
            Console.WriteLine();
            Console.WriteLine($"Index    : {Path.GetFullPath(indexPath)}");
            Console.WriteLine($"Inverted : {Path.GetFullPath(invertedPath)}");
            Console.WriteLine();
            PrintTopTags();
            Console.WriteLine();
        }

        static void LookupLoop()
        {
            if (_inverted.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("No index loaded. Run option 2 first.");
                Console.WriteLine();
                return;
            }
            Console.WriteLine();
            Console.WriteLine("Lookup mode. Type a tag; empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("tag > ");
                Console.ResetColor();
                string? q = Console.ReadLine()?.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(q)) { Console.WriteLine(); return; }
                if (!_inverted.TryGetValue(q, out var matches))
                {
                    Console.WriteLine($"  No images tagged \"{q}\".");
                }
                else
                {
                    Console.WriteLine($"  {matches.Count} image(s) tagged \"{q}\":");
                    foreach (string p in matches) { Console.WriteLine($"    {p}"); }
                }
                Console.WriteLine();
            }
        }

        static void PrintStats()
        {
            Console.WriteLine();
            if (_perImage.Count == 0)
            {
                Console.WriteLine("No index loaded.");
                Console.WriteLine();
                return;
            }
            Console.WriteLine($"  Images indexed : {_perImage.Count}");
            Console.WriteLine($"  Distinct tags  : {_inverted.Count}");
            Console.WriteLine($"  Total tag uses : {_inverted.Sum(kv => kv.Value.Count)}");
            PrintTopTags();
            Console.WriteLine();
        }

        static void PrintTopTags()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Top 15 tags:");
            Console.ResetColor();
            foreach (var kv in _inverted.OrderByDescending(p => p.Value.Count).Take(15))
            {
                Console.WriteLine($"  {kv.Value.Count,4}  {kv.Key}");
            }
        }

        static List<string> TagImage(string path)
        {
            const string instruction =
                "Look at the image and produce a JSON array of 5 to 10 short, lower-case " +
                "descriptive tags. Each tag is one or two words, no punctuation.";

            using Attachment att = new(path);
            MultiTurnConversation chat = new(_model!)
            {
                MaximumCompletionTokens = 200,
                SamplingMode = new RandomSampling { Temperature = 0.4f, TopP = 0.9f, MinP = 0.05f, TopK = 40 },
                SystemPrompt = "You generate concise image tags as a JSON array of strings.",
                Grammar = _jsonArrayGrammar,
            };

            TextGenerationResult r = chat.Submit(new ChatHistory.Message(instruction, att));
            return ParseTags(r.Completion ?? "");
        }

        static List<string> ParseTags(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) { return new List<string>(); }
            try
            {
                string[]? items = JsonSerializer.Deserialize<string[]>(raw);
                if (items == null) { return new List<string>(); }
                var deduped = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var result = new List<string>(Math.Min(items.Length, 10));
                foreach (string item in items)
                {
                    if (string.IsNullOrWhiteSpace(item)) { continue; }
                    string tag = item.Trim().ToLowerInvariant();
                    if (tag.Length > 32) { continue; }
                    if (deduped.Add(tag))
                    {
                        result.Add(tag);
                        if (result.Count >= 10) { break; }
                    }
                }
                return result;
            }
            catch (JsonException)
            {
                return new List<string>();
            }
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a vision model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 4B           (~3 GB VRAM) [Recommended]");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 9B           (~6 GB VRAM)");
            Console.WriteLine("  3 - Google Gemma 4 E2B            (~3 GB VRAM)");
            Console.WriteLine("  4 - Google Gemma 4 E4B            (~5 GB VRAM)");
            Console.Write("\nOr enter a custom model URI / id\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3.5:4b",
                "2" => "qwen3.5:9b",
                "3" => "gemma4:e2b",
                "4" => "gemma4:e4b",
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

        static void WriteIndexJson(Dictionary<string, List<string>> perImage, string path)
        {
            string json = JsonSerializer.Serialize(
                perImage.ToDictionary(kv => kv.Key, kv => kv.Value),
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json, Encoding.UTF8);
        }

        static void WriteInvertedCsv(Dictionary<string, List<string>> inverted, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("tag,count,sample_images");
            foreach (var kv in inverted.OrderByDescending(p => p.Value.Count))
            {
                w.Write(CsvEscape(kv.Key)); w.Write(",");
                w.Write(kv.Value.Count); w.Write(",");
                w.Write(CsvEscape(string.Join(";", kv.Value.Take(5).Select(Path.GetFileName))));
                w.WriteLine();
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
            Console.WriteLine("║      Image Tag Index & Lookup                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Tag images with a VLM under grammar-constrained JSON output,");
            Console.WriteLine("then run tag-based lookup over the index.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Tag single images (interactive)");
            Console.WriteLine("  2 / index    Tag every image in a folder and build the inverted index");
            Console.WriteLine("  3 / lookup   Look up images by tag (after 2)");
            Console.WriteLine("  4 / stats    Index statistics + top tags");
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
