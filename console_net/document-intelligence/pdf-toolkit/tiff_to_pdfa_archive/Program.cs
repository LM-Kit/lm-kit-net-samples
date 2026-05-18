using LMKit.Document.Conversion;
using LMKit.Document.Pdf;
using LMKit.Extraction.Ocr;
using LMKit.TextGeneration;
using System.Diagnostics;
using System.Text;

namespace tiff_to_pdfa_archive
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

            Console.WriteLine("Initialising LM-Kit OCR engine...");
            using LMKitOcr ocr = new();
            Console.WriteLine();

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
                        await ConvertSingle(ocr, cts.Token);
                        break;
                    case "2": case "folder":
                        await ConvertFolder(ocr, cts.Token);
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

        static async Task ConvertSingle(LMKitOcr ocr, CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a multipage TIFF (.tif/.tiff): ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                Console.WriteLine("File not found.");
                return;
            }

            Console.Write("Output PDF path: ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output))
            {
                Console.WriteLine("Output path required.");
                return;
            }

            PdfGenerationOptions options = PromptArchiveOptions();
            await Run(ocr, input, output, options, ct);
        }

        static async Task ConvertFolder(LMKitOcr ocr, CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of TIFF files: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine("Folder not found.");
                return;
            }

            Console.Write("Output directory for PDF/A files: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir))
            {
                Console.WriteLine("Output directory required.");
                return;
            }
            Directory.CreateDirectory(outDir);

            Console.Write("Recurse into subfolders? (y/N): ");
            bool recurse = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");

            PdfGenerationOptions options = PromptArchiveOptions();

            string[] exts = { ".tif", ".tiff" };
            SearchOption opt = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            string[] tiffs = Directory.EnumerateFiles(dir, "*.*", opt)
                .Where(f => exts.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (tiffs.Length == 0)
            {
                Console.WriteLine("No .tif / .tiff files found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Converting {tiffs.Length} TIFF file(s)...");
            string manifestPath = Path.Combine(outDir, "archive_manifest.csv");
            using StreamWriter manifest = new(manifestPath, false, new UTF8Encoding(true));
            manifest.WriteLine("source,output,pages,bytes,elapsed_ms,status");

            int done = 0;
            foreach (string source in tiffs)
            {
                string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(source) + ".pdf");
                Console.WriteLine();
                Console.WriteLine($"  {Path.GetFileName(source)} -> {Path.GetFileName(output)}");
                (bool ok, int pages, long bytes, long ms, string status) = await RunOnce(ocr, source, output, options, ct);
                manifest.Write(Csv(source)); manifest.Write(',');
                manifest.Write(Csv(output)); manifest.Write(',');
                manifest.Write(pages); manifest.Write(',');
                manifest.Write(bytes); manifest.Write(',');
                manifest.Write(ms); manifest.Write(',');
                manifest.WriteLine(Csv(status));
                if (ok) { done++; }
                if (ct.IsCancellationRequested) { break; }
            }

            Console.WriteLine();
            Console.WriteLine($"Converted {done}/{tiffs.Length} file(s). Manifest: {manifestPath}");
            Console.WriteLine();
        }

        static PdfGenerationOptions PromptArchiveOptions()
        {
            Console.Write("PDF/A level [1b|2b|3b] (default 1b): ");
            string lvl = (Console.ReadLine()?.Trim().ToLowerInvariant() ?? "1b");
            PdfGenerationOptions.PdfVersion version = lvl switch
            {
                "2b" => PdfGenerationOptions.PdfVersion.PdfA2b,
                "3b" => PdfGenerationOptions.PdfVersion.PdfA3b,
                _ => PdfGenerationOptions.PdfVersion.PdfA1b,
            };

            Console.Write("OCR languages (comma list, blank = English): ");
            string? langRaw = Console.ReadLine()?.Trim();
            List<Language>? languages = null;
            if (!string.IsNullOrEmpty(langRaw))
            {
                languages = new();
                foreach (string tok in langRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    if (Enum.TryParse<Language>(tok, ignoreCase: true, out Language lang))
                    {
                        languages.Add(lang);
                    }
                    else
                    {
                        Console.WriteLine($"  (unknown language '{tok}', skipped)");
                    }
                }
                if (languages.Count == 0) { languages = null; }
            }

            Console.Write("Page range (blank = all, e.g. \"1-3,5\"): ");
            string? range = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(range)) { range = null; }

            Console.Write("OCR pages in parallel (default 1): ");
            if (!int.TryParse(Console.ReadLine(), out int dop) || dop < 1) { dop = 1; }

            return new PdfGenerationOptions
            {
                Version = version,
                Languages = languages,
                PageRange = range,
                MaxDegreeOfParallelism = dop,
                Creator = "LM-Kit TIFF-to-PDF/A Demo",
                Producer = "LM-Kit.NET",
                EnableOrientationDetection = true,
            };
        }

        static async Task Run(LMKitOcr ocr, string input, string output, PdfGenerationOptions options, CancellationToken ct)
        {
            (bool ok, int pages, long bytes, long ms, string status) = await RunOnce(ocr, input, output, options, ct);
            if (!ok)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {status}");
                Console.ResetColor();
                Console.WriteLine();
                return;
            }
            Console.WriteLine();
            Console.WriteLine($"  Wrote {pages} page(s), {bytes / 1024:N0} KB in {ms} ms.");
            Console.WriteLine($"  Conformance: {options.Version}");
            Console.WriteLine($"  Output: {output}");
            Console.WriteLine();
        }

        static async Task<(bool ok, int pages, long bytes, long ms, string status)> RunOnce(
            LMKitOcr ocr, string input, string output, PdfGenerationOptions options, CancellationToken ct)
        {
            int processed = 0;
            int total = 0;
            void OnProgress(OcrProgressEventArgs e)
            {
                Interlocked.Increment(ref processed);
                total = e.TotalPages;
                Console.Write($"\r  OCR page {processed} / {e.TotalPages} ");
            }

            PdfGenerationOptions effective = new()
            {
                Version = options.Version,
                Languages = options.Languages,
                PageRange = options.PageRange,
                MaxDegreeOfParallelism = options.MaxDegreeOfParallelism,
                Creator = options.Creator,
                Producer = options.Producer,
                EnableOrientationDetection = options.EnableOrientationDetection,
                Progress = new Progress<OcrProgressEventArgs>(OnProgress),
            };

            Stopwatch sw = Stopwatch.StartNew();
            try
            {
                await ImageToSearchablePdf.ConvertAsync(input, ocr, output, effective, ct);
                sw.Stop();
                Console.WriteLine();
                long bytes = new FileInfo(output).Length;
                return (true, total > 0 ? total : processed, bytes, sw.ElapsedMilliseconds, "ok");
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                Console.WriteLine("\n  cancelled.");
                return (false, processed, 0, sw.ElapsedMilliseconds, "cancelled");
            }
            catch (Exception ex)
            {
                sw.Stop();
                Console.WriteLine();
                return (false, processed, 0, sw.ElapsedMilliseconds, ex.Message);
            }
        }

        static string Csv(string s)
        {
            if (s == null) { return ""; }
            bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            string body = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{body}\"" : body;
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
            Console.WriteLine("║      Multipage TIFF to PDF/A Archive             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("OCR every page of a multipage TIFF and produce a searchable PDF/A-1B (archival).");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Convert a single multipage TIFF to PDF/A");
            Console.WriteLine("  2 / folder   Convert every TIFF in a folder (CSV manifest)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
