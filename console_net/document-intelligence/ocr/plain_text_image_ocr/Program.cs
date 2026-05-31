using LMKit.Extraction.Ocr;
using LMKit.Media.Image;
using LMKit.Model;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ocr_demo
{
    internal sealed record CorpusEntry(
        string Source, string MarkdownPath, int CharCount, int LineCount, double ElapsedSeconds);

    internal class Program
    {
        static bool _isDownloading;
        static readonly HashSet<string> ImageExt = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff" };

        static VlmOcr? _ocrMarkdown;
        static VlmOcr? _ocrPlain;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            LM model = LoadModelInteractive();
            _ocrMarkdown = new VlmOcr(model, VlmOcrIntent.Markdown);
            _ocrPlain = new VlmOcr(model, VlmOcrIntent.PlainText);

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
                    case "1": case "single":
                        RunSingleImage();
                        break;
                    case "2": case "corpus":
                        RunCorpusBuilder();
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

        static void RunSingleImage()
        {
            Console.WriteLine();
            VlmOcrIntent intent = PromptIntent();
            VlmOcr ocr = intent == VlmOcrIntent.Markdown ? _ocrMarkdown! : _ocrPlain!;
            Console.WriteLine($"Mode: {intent}. Empty line returns to menu.");
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
                    Stopwatch sw = Stopwatch.StartNew();
                    VlmOcr.VlmOcrResult r = ocr.Run(img);
                    sw.Stop();
                    string text = (r.PageElement?.Text ?? r.TextGeneration?.Completion ?? "").Trim();
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"--- OCR ({intent}, {text.Length} chars, {sw.Elapsed.TotalSeconds:F1}s) ---");
                    Console.ResetColor();
                    Console.WriteLine(text);
                    Console.WriteLine();

                    Console.Write("Write to a file? [y/N]: ");
                    string? save = Console.ReadLine()?.Trim();
                    if (!string.IsNullOrEmpty(save) && save.StartsWith("y", StringComparison.OrdinalIgnoreCase))
                    {
                        string ext = intent == VlmOcrIntent.Markdown ? ".md" : ".txt";
                        string outPath = Path.ChangeExtension(path, ext);
                        File.WriteAllText(outPath, text, Encoding.UTF8);
                        Console.WriteLine($"  wrote {Path.GetFullPath(outPath)}");
                    }
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

        static void RunCorpusBuilder()
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
            Console.Write("Output folder for the corpus [default: <input>/_corpus]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(inDir, "_corpus"); }
            Directory.CreateDirectory(outDir);

            VlmOcrIntent intent = PromptIntent();
            VlmOcr ocr = intent == VlmOcrIntent.Markdown ? _ocrMarkdown! : _ocrPlain!;
            string ext = intent == VlmOcrIntent.Markdown ? ".md" : ".txt";

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
            Console.WriteLine($"OCRing {images.Length} image(s) in {intent} mode ...");
            Console.WriteLine();
            var entries = new List<CorpusEntry>(images.Length);
            for (int i = 0; i < images.Length; i++)
            {
                string p = images[i];
                try
                {
                    using ImageBuffer img = ImageBuffer.LoadAsRGB(p);
                    Stopwatch sw = Stopwatch.StartNew();
                    VlmOcr.VlmOcrResult result = ocr.Run(img);
                    sw.Stop();
                    string text = (result.PageElement?.Text ?? result.TextGeneration?.Completion ?? "").Trim();
                    string stem = Path.GetFileNameWithoutExtension(p);
                    string outPath = Path.Combine(outDir, stem + ext);
                    File.WriteAllText(outPath, text, Encoding.UTF8);

                    var entry = new CorpusEntry(
                        Source: p,
                        MarkdownPath: outPath,
                        CharCount: text.Length,
                        LineCount: text.Count(c => c == '\n') + 1,
                        ElapsedSeconds: sw.Elapsed.TotalSeconds);
                    entries.Add(entry);
                    Console.WriteLine($"  [{i + 1}/{images.Length}] {Path.GetFileName(p),-40} {entry.CharCount,6} chars  {entry.ElapsedSeconds,5:F1}s");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }

            string corpusPath = Path.Combine(outDir, intent == VlmOcrIntent.Markdown ? "corpus.md" : "corpus.txt");
            WriteStitchedCorpus(entries, corpusPath, intent);
            string indexPath = Path.Combine(outDir, "corpus_index.csv");
            WriteIndex(entries, indexPath);

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Corpus summary");
            Console.ResetColor();
            Console.WriteLine($"  Documents       : {entries.Count}");
            Console.WriteLine($"  Total chars     : {entries.Sum(e => (long)e.CharCount):N0}");
            Console.WriteLine($"  Total OCR time  : {entries.Sum(e => e.ElapsedSeconds):F1} s");
            Console.WriteLine();
            Console.WriteLine($"Per-file output : {Path.GetFullPath(outDir)}");
            Console.WriteLine($"Stitched corpus : {Path.GetFullPath(corpusPath)}");
            Console.WriteLine($"Corpus index    : {Path.GetFullPath(indexPath)}");
            Console.WriteLine();
        }

        static VlmOcrIntent PromptIntent()
        {
            Console.WriteLine("Pick OCR intent:");
            Console.WriteLine("  1 - Markdown  (preserves headings, tables, lists) [Recommended]");
            Console.WriteLine("  2 - Plain text");
            Console.Write("> ");
            string? c = Console.ReadLine()?.Trim();
            return c switch
            {
                "2" or "plain" => VlmOcrIntent.PlainText,
                _ => VlmOcrIntent.Markdown,
            };
        }

        static void WriteStitchedCorpus(List<CorpusEntry> entries, string path, VlmOcrIntent intent)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            if (intent == VlmOcrIntent.Markdown)
            {
                w.WriteLine("# OCR Corpus");
                w.WriteLine();
                w.WriteLine($"- Built : {DateTime.UtcNow:o}");
                w.WriteLine($"- Files : {entries.Count}");
                w.WriteLine();
                foreach (var e in entries)
                {
                    w.WriteLine($"## {Path.GetFileName(e.Source)}");
                    w.WriteLine();
                    w.WriteLine($"_Source: `{e.Source}`_");
                    w.WriteLine();
                    if (File.Exists(e.MarkdownPath)) { w.WriteLine(File.ReadAllText(e.MarkdownPath, Encoding.UTF8)); }
                    w.WriteLine();
                    w.WriteLine("---");
                    w.WriteLine();
                }
            }
            else
            {
                foreach (var e in entries)
                {
                    w.WriteLine($"=== {Path.GetFileName(e.Source)} ===");
                    if (File.Exists(e.MarkdownPath)) { w.WriteLine(File.ReadAllText(e.MarkdownPath, Encoding.UTF8)); }
                    w.WriteLine();
                }
            }
        }

        static void WriteIndex(List<CorpusEntry> entries, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("source,output,char_count,line_count,elapsed_seconds");
            foreach (var e in entries)
            {
                w.Write(CsvEscape(e.Source)); w.Write(",");
                w.Write(CsvEscape(e.MarkdownPath)); w.Write(",");
                w.Write(e.CharCount); w.Write(",");
                w.Write(e.LineCount); w.Write(",");
                w.Write(e.ElapsedSeconds.ToString("F2", CultureInfo.InvariantCulture));
                w.WriteLine();
            }
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select an OCR model:");
            Console.WriteLine("  1 - paddleocr-vl-1.6:0.9b   (compact, fast)   [Recommended]");
            Console.WriteLine("  2 - glm-ocr             (higher accuracy)");
            Console.WriteLine("  3 - lightonocr-2:1b     (small, fast)");
            Console.Write("\nOr enter a custom model URI / id\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "paddleocr-vl-1.6:0.9b",
                "2" => "glm-ocr",
                "3" => "lightonocr-2:1b",
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

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      OCR Markdown Corpus Builder                 ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Run VLM-OCR on a folder of scans, write one Markdown (or plain-text)");
            Console.WriteLine("file per image plus a stitched corpus and an index CSV.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / single   OCR a single image (interactive)");
            Console.WriteLine("  2 / corpus   Build a Markdown corpus from a folder");
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
