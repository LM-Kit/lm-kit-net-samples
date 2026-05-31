using LMKit.Document.Layout;
using LMKit.Extraction.Ocr;
using LMKit.Media.Image;
using LMKit.Model;
using System.Text;

namespace page_layout_inspector
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

            Console.WriteLine("Loading paddleocr-vl-1.6:0.9b ...");
            using LM model = LM.LoadFromModelID("paddleocr-vl-1.6:0.9b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();
            VlmOcr ocr = new(model, VlmOcrIntent.Markdown);

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
                    case "1": case "image":
                        InspectImage(ocr);
                        break;
                    case "2": case "folder":
                        InspectFolder(ocr);
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

        static void InspectImage(VlmOcr ocr)
        {
            Console.WriteLine();
            Console.Write("Path to a page image (PNG/JPG): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) { Console.WriteLine("File not found."); return; }

            Console.Write("Lines to preview (default 10): ");
            int.TryParse(Console.ReadLine(), out int preview);
            if (preview <= 0) { preview = 10; }

            Console.Write("Write a CSV of line bounds? (path or blank to skip): ");
            string? csvPath = Console.ReadLine()?.Trim().Trim('"');

            Analyze(ocr, path, preview, csvPath);
        }

        static void InspectFolder(VlmOcr ocr)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of page images: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output directory for per-file line CSVs: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            string[] exts = { ".png", ".jpg", ".jpeg", ".bmp", ".webp", ".tif", ".tiff" };
            string[] files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToArray();
            if (files.Length == 0) { Console.WriteLine("No images found."); return; }

            foreach (string p in files)
            {
                string csvPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".lines.csv");
                Analyze(ocr, p, previewLines: 0, csvPath);
            }
            Console.WriteLine();
            Console.WriteLine($"Processed {files.Length} image(s). CSVs in {outDir}");
            Console.WriteLine();
        }

        static void Analyze(VlmOcr ocr, string path, int previewLines, string? csvPath)
        {
            try
            {
                using ImageBuffer img = ImageBuffer.LoadAsRGB(path);
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(path)} ({img.Width}x{img.Height})");

                VlmOcr.VlmOcrResult r = ocr.Run(img);
                PageElement? page = r.PageElement;
                if (page == null) { Console.WriteLine("    (no layout produced)"); return; }

                List<LineElement> lines = page.DetectLines();
                List<ParagraphElement> paras = page.DetectParagraphs();
                Console.WriteLine($"    text elements : {page.TextElements.Count()}");
                Console.WriteLine($"    lines         : {lines.Count}");
                Console.WriteLine($"    paragraphs    : {paras.Count}");

                int sample = Math.Min(previewLines, lines.Count);
                for (int j = 0; j < sample; j++)
                {
                    LineElement line = lines[j];
                    string text = line.Text.Trim();
                    if (text.Length > 80) { text = text.Substring(0, 79) + "…"; }
                    Console.WriteLine($"      [{line.Bounds.Top:F0},{line.Bounds.Left:F0}] {text}");
                }

                if (!string.IsNullOrWhiteSpace(csvPath))
                {
                    using StreamWriter csv = new(csvPath, false, new UTF8Encoding(true));
                    csv.WriteLine("index,top,left,bottom,right,text");
                    for (int j = 0; j < lines.Count; j++)
                    {
                        LineElement line = lines[j];
                        csv.Write(j); csv.Write(',');
                        csv.Write($"{line.Bounds.Top:F1}"); csv.Write(',');
                        csv.Write($"{line.Bounds.Left:F1}"); csv.Write(',');
                        csv.Write($"{line.Bounds.Bottom:F1}"); csv.Write(',');
                        csv.Write($"{line.Bounds.Right:F1}"); csv.Write(',');
                        csv.WriteLine(Csv(line.Text));
                    }
                    Console.WriteLine($"    CSV: {csvPath}");
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"    [error] {ex.Message}");
                Console.ResetColor();
            }
        }

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
            Console.WriteLine("║      Page Layout Inspector                       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Run VLM-OCR and inspect the layout tree: lines, paragraphs, bounding boxes.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / image    Inspect a single page image (preview + optional CSV)");
            Console.WriteLine("  2 / folder   Inspect every image in a folder (one CSV per file)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
