using LMKit.Media.Image;
using LMKit.Model;
using LMKit.TextAnalysis;
using System.Globalization;
using System.Text;

namespace image_classification
{
    internal sealed record SortDecision(
        string SourcePath, string Category, float Confidence, string DestinationPath);

    internal class Program
    {
        static bool _isDownloading;

        static readonly string[] DefaultCategories =
        {
            "product photo", "people photo", "screenshot", "document scan",
            "chart or diagram", "outdoor scene", "indoor scene", "food",
            "vehicle", "other",
        };

        static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff" };

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            WriteHeader();

            LM model = LoadModelInteractive();
            if (!model.HasVision)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\nERROR: model does not support vision.");
                Console.ResetColor();
                return;
            }
            Categorization classifier = new(model);

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
                        RunLiveMode(classifier);
                        break;
                    case "2": case "folder":
                        RunFolderMode(classifier);
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

        static void RunLiveMode(Categorization classifier)
        {
            Console.WriteLine();
            string[] categories = PromptCategories();
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
                try
                {
                    using ImageBuffer img = ImageBuffer.LoadAsRGB(path);
                    int idx = classifier.GetBestCategory(categories, img);
                    string label = idx >= 0 && idx < categories.Length ? categories[idx] : "(unknown)";
                    float confidence = classifier.Confidence;
                    Console.WriteLine($"  -> {label}  ({confidence:P0})");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {ex.Message}");
                    Console.ResetColor();
                }
                Console.WriteLine();
            }
        }

        static void RunFolderMode(Categorization classifier)
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
            string[] categories = PromptCategories();
            Console.Write("Output folder for sorted copies [default: <input>/_sorted]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(inDir, "_sorted"); }
            Directory.CreateDirectory(outDir);

            Console.Write("Minimum confidence threshold (below -> _uncertain/) [0.0]: ");
            double minConfidence = double.TryParse(Console.ReadLine()?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double mc) ? mc : 0.0;
            Console.Write("Move files instead of copy? [y/N]: ");
            bool move = (Console.ReadLine()?.Trim() ?? "").StartsWith("y", StringComparison.OrdinalIgnoreCase);

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
            Console.WriteLine($"Sorting {images.Length} image(s) into {categories.Length} categor(y/ies) ...");
            Console.WriteLine();
            var decisions = new List<SortDecision>();
            var perCategory = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < images.Length; i++)
            {
                string p = images[i];
                try
                {
                    using ImageBuffer img = ImageBuffer.LoadAsRGB(p);
                    int idx = classifier.GetBestCategory(categories, img);
                    float confidence = classifier.Confidence;
                    string category = idx >= 0 && idx < categories.Length ? categories[idx] : "other";
                    string bucket = confidence < minConfidence ? "_uncertain" : SafeFolderName(category);

                    string destDir = Path.Combine(outDir, bucket);
                    Directory.CreateDirectory(destDir);
                    string destPath = UniquePath(Path.Combine(destDir, Path.GetFileName(p)));
                    if (move) { File.Move(p, destPath); }
                    else      { File.Copy(p, destPath, overwrite: false); }

                    decisions.Add(new SortDecision(p, category, confidence, destPath));
                    perCategory.TryGetValue(bucket, out int n);
                    perCategory[bucket] = n + 1;

                    Console.WriteLine($"  [{i + 1,3}/{images.Length}] {Path.GetFileName(p),-40} -> {bucket,-22} ({confidence:P0})");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Sort summary");
            Console.ResetColor();
            foreach (var kv in perCategory.OrderByDescending(p => p.Value))
            {
                Console.WriteLine($"  {kv.Key,-22} {kv.Value,6}");
            }

            string manifestPath = Path.Combine(outDir, "sort_manifest.csv");
            WriteManifest(decisions, manifestPath);
            Console.WriteLine();
            Console.WriteLine($"Manifest: {Path.GetFullPath(manifestPath)}");
            Console.WriteLine();
        }

        static string[] PromptCategories()
        {
            Console.WriteLine();
            Console.WriteLine("Pick category list:");
            Console.WriteLine("  1 - Default 10 categories (product, people, screenshot, document scan, ...)");
            Console.WriteLine("  2 - Enter your own (one per line; blank line ends)");
            Console.WriteLine("  3 - Read from a file (one category per line)");
            Console.Write("> ");
            string? c = Console.ReadLine()?.Trim();
            switch (c)
            {
                case "2":
                    Console.WriteLine("Enter categories, one per line. Blank line ends.");
                    var custom = new List<string>();
                    while (true)
                    {
                        Console.Write($"  cat{custom.Count + 1} > ");
                        string? line = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(line)) { break; }
                        custom.Add(line);
                    }
                    return custom.Count > 0 ? custom.ToArray() : DefaultCategories;
                case "3":
                    Console.Write("Path to categories file: ");
                    string? path = Console.ReadLine()?.Trim().Trim('"');
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    {
                        return File.ReadAllLines(path)
                                   .Select(l => l.Trim())
                                   .Where(l => l.Length > 0 && !l.StartsWith('#'))
                                   .ToArray();
                    }
                    Console.WriteLine("File not found; using defaults.");
                    return DefaultCategories;
                default:
                    return DefaultCategories;
            }
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a vision model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 4B           (~3 GB VRAM) [Recommended]");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 9B           (~6 GB VRAM)");
            Console.WriteLine("  3 - Google Gemma 4 E2B            (~3 GB VRAM)");
            Console.WriteLine("  4 - Google Gemma 4 E4B            (~5 GB VRAM)");
            Console.WriteLine("  5 - GLM 4.6V Flash                (~6 GB VRAM)");
            Console.Write("\nOr enter a custom model URI / id\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3.5:4b",
                "2" => "qwen3.5:9b",
                "3" => "gemma4:e2b",
                "4" => "gemma4:e4b",
                "5" => "glm-4.6v-flash",
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

        static string SafeFolderName(string label)
        {
            var sb = new StringBuilder(label.Length);
            foreach (char c in label)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_');
            }
            return sb.ToString().Trim().Replace(' ', '_');
        }

        static string UniquePath(string desired)
        {
            if (!File.Exists(desired)) { return desired; }
            string dir = Path.GetDirectoryName(desired)!;
            string name = Path.GetFileNameWithoutExtension(desired);
            string ext = Path.GetExtension(desired);
            for (int i = 1; ; i++)
            {
                string candidate = Path.Combine(dir, $"{name}-{i}{ext}");
                if (!File.Exists(candidate)) { return candidate; }
            }
        }

        static void WriteManifest(List<SortDecision> decisions, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("source,category,confidence,destination");
            foreach (var d in decisions)
            {
                w.Write(CsvEscape(d.SourcePath)); w.Write(",");
                w.Write(CsvEscape(d.Category)); w.Write(",");
                w.Write(d.Confidence.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(CsvEscape(d.DestinationPath));
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
            Console.WriteLine("║      Photo Auto-Sorter (Zero-Shot)               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Classify each image into a category from a caller-defined list,");
            Console.WriteLine("then copy or move it into a per-category folder. Built on Categorization.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Classify single images (interactive)");
            Console.WriteLine("  2 / folder   Sort a folder of images into category sub-folders + manifest");
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
