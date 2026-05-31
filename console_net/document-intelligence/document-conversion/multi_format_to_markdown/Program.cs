using LMKit.Document.Conversion;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Text;

namespace multi_format_to_markdown
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

            Console.WriteLine("Loading paddleocr-vl-1.6:0.9b (used for scanned / image pages) ...");
            using LM ocrModel = LM.LoadFromModelID("paddleocr-vl-1.6:0.9b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            DocumentToMarkdown converter = new(ocrModel);

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
                    case "1": case "file":
                        ConvertSingle(converter, ocrModel);
                        break;
                    case "2": case "folder":
                        ConvertFolder(converter, ocrModel);
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

        static DocumentToMarkdownOptions PromptOptions(LM ocrModel)
        {
            Console.Write("Strategy [hybrid|text|vlmocr] (default hybrid): ");
            string s = (Console.ReadLine()?.Trim().ToLowerInvariant() ?? "hybrid");
            DocumentToMarkdownStrategy strategy = s switch
            {
                "text" => DocumentToMarkdownStrategy.TextExtraction,
                "vlmocr" => DocumentToMarkdownStrategy.VlmOcr,
                _ => DocumentToMarkdownStrategy.Hybrid,
            };

            Console.Write("Include page separators? (Y/n): ");
            bool separators = (Console.ReadLine()?.Trim().ToLowerInvariant() != "n");

            return new DocumentToMarkdownOptions
            {
                Strategy = strategy,
                OcrEngine = new VlmOcr(ocrModel, VlmOcrIntent.Markdown),
                OcrImageParallelism = 2,
                IncludePageSeparators = separators,
            };
        }

        static void ConvertSingle(DocumentToMarkdown converter, LM ocrModel)
        {
            Console.WriteLine();
            Console.Write("Path to a document (.pdf/.docx/.html/.eml/.txt/image): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }

            DocumentToMarkdownOptions options = PromptOptions(ocrModel);

            Console.Write("Output Markdown path (blank = next to input as .md): ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { output = Path.ChangeExtension(path, ".md"); }

            Convert(converter, path, output, options);
        }

        static void ConvertFolder(DocumentToMarkdown converter, LM ocrModel)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of documents: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output directory for .md files: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            DocumentToMarkdownOptions options = PromptOptions(ocrModel);

            string[] exts = { ".pdf", ".docx", ".doc", ".html", ".htm", ".eml", ".txt", ".md", ".png", ".jpg", ".jpeg", ".tif", ".tiff", ".bmp", ".webp" };
            string[] files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No supported files found."); return; }

            int done = 0;
            foreach (string p in files)
            {
                string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".md");
                if (Convert(converter, p, output, options)) { done++; }
            }
            Console.WriteLine();
            Console.WriteLine($"Converted {done}/{files.Length} document(s).");
            Console.WriteLine();
        }

        static bool Convert(DocumentToMarkdown converter, string input, string output, DocumentToMarkdownOptions options)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(input)}");
                DocumentToMarkdownResult result = converter.Convert(input, options);
                Console.WriteLine($"      strategy : {result.RequestedStrategy} -> {result.EffectiveStrategy}");
                Console.WriteLine($"      certainty: {result.Certainty:F2}  pages: {result.Pages.Count}  chars: {result.Markdown.Length}  time: {result.Elapsed.TotalSeconds:F1}s");
                File.WriteAllText(output, result.Markdown, Encoding.UTF8);
                Console.WriteLine($"      -> {output}");
                return true;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"      [error] {ex.Message}");
                Console.ResetColor();
                return false;
            }
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
            Console.WriteLine("║      Multi-Format to Markdown                    ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Convert PDFs, Office docs, HTML, EML, and images to Markdown (text + OCR).");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Convert a single document");
            Console.WriteLine("  2 / folder   Convert every supported file in a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
