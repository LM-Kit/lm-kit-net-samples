using LMKit.Document.Conversion;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Text;

namespace email_archive_to_markdown
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

            Console.WriteLine("Loading paddleocr-vl-1.6:0.9b (used for embedded scans / attachments) ...");
            using LM ocrModel = LM.LoadFromModelID("paddleocr-vl-1.6:0.9b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();
            DocumentToMarkdown converter = new(ocrModel);
            DocumentToMarkdownOptions options = new()
            {
                Strategy = DocumentToMarkdownStrategy.Hybrid,
                OcrEngine = new VlmOcr(ocrModel, VlmOcrIntent.Markdown),
            };

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
                        ConvertSingle(converter, options);
                        break;
                    case "2": case "archive":
                        ConvertArchive(converter, options);
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

        static void ConvertSingle(DocumentToMarkdown converter, DocumentToMarkdownOptions options)
        {
            Console.WriteLine();
            Console.Write("Path to a .eml or .mbox file: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }

            Console.Write("Output Markdown path (blank = next to input as .md): ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { output = Path.ChangeExtension(input, ".md"); }

            if (Convert(converter, input, output, options))
            {
                Console.Write("Show preview? (y/N): ");
                if (Console.ReadLine()?.Trim().ToLowerInvariant() == "y")
                {
                    string md = File.ReadAllText(output, Encoding.UTF8);
                    Console.WriteLine();
                    Console.WriteLine("--- preview ---");
                    Console.WriteLine(md.Length > 800 ? md.Substring(0, 800) + "..." : md);
                    Console.WriteLine();
                }
            }
        }

        static void ConvertArchive(DocumentToMarkdown converter, DocumentToMarkdownOptions options)
        {
            Console.WriteLine();
            Console.Write("Path to a folder containing .eml / .mbox files: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output directory for .md files: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);
            Console.Write("Also write a combined archive.md index? (Y/n): ");
            bool combined = (Console.ReadLine()?.Trim().ToLowerInvariant() != "n");

            string[] exts = { ".eml", ".mbox" };
            string[] files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No .eml or .mbox files found."); return; }

            StreamWriter? archive = null;
            if (combined)
            {
                archive = new StreamWriter(Path.Combine(outDir, "archive.md"), false, new UTF8Encoding(true));
                archive.WriteLine("# Email Archive");
                archive.WriteLine();
            }

            int done = 0;
            foreach (string p in files)
            {
                string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".md");
                if (!Convert(converter, p, output, options)) { continue; }
                done++;

                if (archive != null)
                {
                    string md = File.ReadAllText(output, Encoding.UTF8);
                    archive.WriteLine($"## {Path.GetFileNameWithoutExtension(p)}");
                    archive.WriteLine();
                    archive.WriteLine(md);
                    archive.WriteLine();
                    archive.WriteLine("---");
                    archive.WriteLine();
                }
            }
            archive?.Dispose();
            Console.WriteLine();
            Console.WriteLine($"Converted {done}/{files.Length} email(s).");
            if (combined) { Console.WriteLine($"Combined index: {Path.Combine(outDir, "archive.md")}"); }
            Console.WriteLine();
        }

        static bool Convert(DocumentToMarkdown converter, string input, string output, DocumentToMarkdownOptions options)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(input)}");
                DocumentToMarkdownResult r = converter.Convert(input, options);
                Console.WriteLine($"      strategy: {r.RequestedStrategy} -> {r.EffectiveStrategy}   certainty: {r.Certainty:F2}   {r.Markdown.Length} chars   {r.Elapsed.TotalSeconds:F1}s");
                File.WriteAllText(output, r.Markdown, Encoding.UTF8);
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
            Console.WriteLine("║      Email Archive to Markdown                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Convert .eml / .mbox into searchable Markdown with attachments and OCR.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file      Convert a single .eml or .mbox");
            Console.WriteLine("  2 / archive   Convert every email in a folder (optional combined index)");
            Console.WriteLine("  q / quit      Exit");
            Console.WriteLine();
        }
    }
}
