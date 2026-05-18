using LMKit.Document.Pdf;
using LMKit.Media.Image;
using System.Diagnostics;
using System.Text;

namespace pdf_to_multipage_tiff
{
    internal class Program
    {
        static async Task Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
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

                using var cts = new CancellationTokenSource();
                using var keyHandler = HookCancel(cts);

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "file":
                        await ConvertSingle(cts.Token);
                        break;
                    case "2": case "folder":
                        await ConvertFolder(cts.Token);
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

        static async Task ConvertSingle(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }

            Console.Write("Output TIFF path: ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine("Output path required."); return; }

            (double zoom, bool grayscale, string? range) = PromptRenderSettings();
            await Run(input, output, new PdfRenderOptions
            {
                Zoom = zoom,
                PixelFormat = grayscale ? ImagePixelFormat.GRAY8 : ImagePixelFormat.RGB24,
                PageRange = range,
            }, ct);
        }

        static async Task ConvertFolder(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of PDFs: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }

            Console.Write("Output directory for .tif files: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            (double zoom, bool grayscale, string? range) = PromptRenderSettings();

            string[] pdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pdfs.Length == 0) { Console.WriteLine("No .pdf files found."); return; }

            Console.WriteLine();
            Console.WriteLine($"Converting {pdfs.Length} file(s)...");
            foreach (string p in pdfs)
            {
                string outPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(p) + ".tif");
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(p)}");
                await Run(p, outPath, new PdfRenderOptions
                {
                    Zoom = zoom,
                    PixelFormat = grayscale ? ImagePixelFormat.GRAY8 : ImagePixelFormat.RGB24,
                    PageRange = range,
                }, ct);
                if (ct.IsCancellationRequested) { return; }
            }
        }

        static (double zoom, bool grayscale, string? range) PromptRenderSettings()
        {
            Console.Write("Zoom (1.0=72ppi, 2.0=144ppi, default 2.0): ");
            if (!double.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zoom) || zoom <= 0)
            {
                zoom = 2.0;
            }
            Console.Write("Grayscale? (y/N, recommended for archival scans): ");
            bool grayscale = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");
            Console.Write("Page range (blank = all, e.g. \"1-3,5\"): ");
            string? range = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(range)) { range = null; }
            return (zoom, grayscale, range);
        }

        static async Task Run(string input, string output, PdfRenderOptions options, CancellationToken ct)
        {
            int sourcePages;
            try { sourcePages = await PdfInfo.GetPageCountAsync(input, cancellationToken: ct); }
            catch (Exception ex) { Console.WriteLine($"  [error] {ex.Message}"); return; }
            Console.WriteLine($"  Source pages : {sourcePages}");
            Console.WriteLine($"  Zoom         : {options.Zoom}x ({options.Zoom * 72:F0} ppi), {options.PixelFormat}");
            Console.WriteLine($"  Page range   : {options.PageRange ?? "(all)"}");

            var progress = new Progress<PdfRenderProgressEventArgs>(e =>
            {
                Console.Write($"\r  rendering page {e.PageIndex + 1} / {e.TotalPages}... ");
            });

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await PdfRenderer.SavePagesAsMultipageTiffAsync(input, output, options, progress, ct);
            }
            catch (OperationCanceledException) { Console.WriteLine("\n  cancelled."); return; }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  [error] {ex.Message}");
                Console.ResetColor();
                return;
            }
            sw.Stop();
            FileInfo fi = new(output);
            Console.WriteLine($"\r  done in {sw.Elapsed:mm\\:ss\\.ff}. Output: {output} ({fi.Length / 1024:N0} KB)");
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
            Console.WriteLine("║      PDF to Multi-page TIFF Archive              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Pack every rendered page into a single multi-page TIFF for archival.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Convert a single PDF to one multi-page TIFF");
            Console.WriteLine("  2 / folder   Convert every PDF in a folder (one TIFF per source)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
