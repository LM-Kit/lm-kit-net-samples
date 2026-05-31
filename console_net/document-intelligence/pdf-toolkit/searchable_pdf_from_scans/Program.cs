using LMKit.Document.Pdf;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace searchable_pdf_from_scans
{
    internal class Program
    {
        static bool _isDownloading;
        static int _pagesDone;
        static int _pagesTotal;

        static async Task Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            string modelId = PromptModel();
            Console.WriteLine($"Loading {modelId} ...");
            using LM ocrModel = LM.LoadFromModelID(modelId,
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();
            VlmOcr ocrEngine = new(ocrModel, VlmOcrIntent.Markdown);

            PrintMenu();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                using var cts = new CancellationTokenSource();
                using var keyHandler = HookCancel(cts);

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "file":
                        await ConvertSingle(ocrEngine, cts.Token);
                        break;
                    case "2": case "folder":
                        await ConvertFolder(ocrEngine, cts.Token);
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

        static string PromptModel()
        {
            Console.WriteLine("Pick a VLM-OCR model:");
            Console.WriteLine("  1  paddleocr-vl-1.6:0.9b   (fast, low-VRAM, default)");
            Console.WriteLine("  2  glm-ocr             (higher accuracy)");
            Console.WriteLine("  3  lightonocr-2:1b     (balanced)");
            Console.Write("Choice [1-3] (default 1): ");
            string? c = Console.ReadLine()?.Trim();
            return c switch
            {
                "2" => "glm-ocr",
                "3" => "lightonocr-2:1b",
                _ => "paddleocr-vl-1.6:0.9b",
            };
        }

        static async Task ConvertSingle(VlmOcr ocrEngine, CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a scanned PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }
            Console.Write("Output PDF path: ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine("Output path required."); return; }

            await Run(input, output, ocrEngine, ct);
        }

        static async Task ConvertFolder(VlmOcr ocrEngine, CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of scanned PDFs: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            string[] pdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pdfs.Length == 0) { Console.WriteLine("No .pdf files found."); return; }

            foreach (string p in pdfs)
            {
                string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".searchable.pdf");
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(p)} -> {Path.GetFileName(output)}");
                await Run(p, output, ocrEngine, ct);
                if (ct.IsCancellationRequested) { return; }
            }
        }

        static async Task Run(string input, string output, VlmOcr ocrEngine, CancellationToken ct)
        {
            int pageCount;
            try { pageCount = await PdfInfo.GetPageCountAsync(input, cancellationToken: ct); }
            catch (Exception ex) { Console.WriteLine($"  [error] {ex.Message}"); return; }
            _pagesTotal = pageCount;
            _pagesDone = 0;
            Console.WriteLine($"  Pages: {pageCount}");

            PdfSearchableMakerOptions options = new()
            {
                TextPageHandling = PdfSearchableMaker.TextPageHandling.Skip,
                TextDetectionStrategy = PdfSearchableMaker.TextDetectionStrategy.HasText,
                MaxDegreeOfParallelism = 1,
                Progress = new Progress<OcrProgressEventArgs>(e =>
                {
                    Interlocked.Increment(ref _pagesDone);
                    Console.WriteLine($"  page {_pagesDone}/{_pagesTotal} OCRed");
                }),
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await PdfSearchableMaker.ConvertToFileAsync(input, ocrEngine, output, options, ct);
            }
            catch (OperationCanceledException) { Console.WriteLine("  cancelled."); return; }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
                return;
            }
            sw.Stop();
            FileInfo fi = new(output);
            Console.WriteLine($"  Done in {sw.Elapsed:mm\\:ss\\.ff}. Output: {fi.Length / 1024:N0} KB");
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

        static IDisposable HookCancel(CancellationTokenSource cts)
        {
            ConsoleCancelEventHandler h = (_, e) => { e.Cancel = true; cts.Cancel(); };
            Console.CancelKeyPress += h;
            return new Disposable(() => Console.CancelKeyPress -= h);
        }

        sealed class Disposable : IDisposable
        {
            readonly Action _onDispose;
            public Disposable(Action onDispose) { _onDispose = onDispose; }
            public void Dispose() => _onDispose();
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Searchable PDF from Scans                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("OCR scanned PDFs and write back a PDF with a real text layer.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Convert a single scanned PDF");
            Console.WriteLine("  2 / folder   Convert every PDF in a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
