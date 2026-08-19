using LMKit.Document.Pdf;
using System.Diagnostics;
using System.Text;

namespace pdf_to_pdfa_converter
{
    internal class Program
    {
        static async Task Main()
        {
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
            Console.Write("Path to a PDF file: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                Console.WriteLine("File not found.");
                return;
            }

            Console.Write($"Output path [{DefaultOutputPath(input)}]: ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output))
            {
                output = DefaultOutputPath(input);
            }

            PdfAConversionOptions options = PromptOptions();
            await Run(input, output, options, ct);
        }

        static async Task ConvertFolder(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of PDF files: ");
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

            PdfAConversionOptions options = PromptOptions();

            string[] files = Directory.GetFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly);
            Console.WriteLine($"Found {files.Length} PDF file(s).");
            Console.WriteLine();

            int converted = 0;
            int failed = 0;

            foreach (string file in files)
            {
                if (ct.IsCancellationRequested) { break; }

                string output = Path.Combine(outDir, Path.GetFileNameWithoutExtension(file) + "_pdfa.pdf");

                try
                {
                    await Run(file, output, options, ct);
                    converted++;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception e)
                {
                    failed++;
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  {Path.GetFileName(file)}: {e.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.WriteLine($"Done: {converted} converted, {failed} failed.");
        }

        static async Task Run(string input, string output, PdfAConversionOptions options, CancellationToken ct)
        {
            var stopwatch = Stopwatch.StartNew();

            PdfAConversionReport report = await PdfAConverter.ConvertToFileAsync(input, output, options, ct);

            stopwatch.Stop();

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"  {Path.GetFileName(input)} -> {Path.GetFileName(output)}");
            Console.ResetColor();
            Console.WriteLine($"  Level            : {report.Level}");
            Console.WriteLine($"  Conforms         : {report.Conforms}");
            Console.WriteLine($"  Pages            : {report.PageCount}");
            Console.WriteLine($"  Strategy         : {(report.UsedRasterFallback ? "raster fallback (rendered pages + invisible text layer)" : "conforming rewrite (content preserved)")}");

            if (report.FeaturesDetected != PdfAFeatures.None)
            {
                Console.WriteLine($"  Features detected: {report.FeaturesDetected}");
            }

            if (report.FixesApplied != PdfAFixes.None)
            {
                Console.WriteLine($"  Fixes applied    : {report.FixesApplied}");
            }

            if (report.EncryptionRemoved)
            {
                Console.WriteLine("  Encryption removed (PDF/A prohibits encryption).");
            }

            if (report.UnresolvedViolations != PdfAFeatures.None)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"  Unresolved       : {report.UnresolvedViolations}");
                Console.ResetColor();
            }

            Console.WriteLine($"  Elapsed          : {stopwatch.ElapsedMilliseconds} ms");
            Console.WriteLine();
        }

        static PdfAConversionOptions PromptOptions()
        {
            var options = new PdfAConversionOptions();

            Console.Write("Conformance level: 1=PDF/A-1b, 2=PDF/A-2b (default), 3=PDF/A-3b: ");
            string? level = Console.ReadLine()?.Trim();

            options.Level = level switch
            {
                "1" => PdfAConformanceLevel.PdfA1b,
                "3" => PdfAConformanceLevel.PdfA3b,
                _ => PdfAConformanceLevel.PdfA2b,
            };

            Console.Write("For content that still can't conform: 1=rasterize the rest (default), 2=fail, 3=report only: ");
            string? fallback = Console.ReadLine()?.Trim();

            options.Fallback = fallback switch
            {
                "2" => PdfAConversionOptions.FallbackBehavior.Fail,
                "3" => PdfAConversionOptions.FallbackBehavior.ReportOnly,
                _ => PdfAConversionOptions.FallbackBehavior.Rasterize,
            };

            Console.Write("Password (empty for unencrypted source): ");
            options.Password = Console.ReadLine()?.Trim() ?? "";

            return options;
        }

        static string DefaultOutputPath(string input)
        {
            string dir = Path.GetDirectoryName(input) ?? ".";
            return Path.Combine(dir, Path.GetFileNameWithoutExtension(input) + "_pdfa.pdf");
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
            Console.WriteLine("║        PDF to PDF/A Converter (ISO 19005)        ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Convert existing PDFs to the PDF/A archival format: fonts embedded,");
            Console.WriteLine("colours calibrated, prohibited constructs removed, metadata rebuilt.");
            Console.WriteLine("Runs fully on-device; no model download required.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Convert a single PDF to PDF/A");
            Console.WriteLine("  2 / folder   Convert every PDF in a folder");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine("  ? / help     Show this menu");
            Console.WriteLine();
        }
    }
}
