using LMKit.Document.Pdf;
using System.Text;

namespace pdf_splitter_by_range
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
                    case "1": case "ranges":
                        await SplitByRanges(cts.Token);
                        break;
                    case "2": case "every":
                        await SplitEveryNPages(cts.Token);
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

        static async Task SplitByRanges(CancellationToken ct)
        {
            (string input, int totalPages) = await PromptInput(ct);
            if (input == null!) { return; }

            Console.WriteLine();
            Console.WriteLine("Range syntax (1-based): \"1-3\", \"1,3,5\", \"2-4,7\". Blank line ends the list.");
            List<string> ranges = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [part {ranges.Count + 1}] ");
                Console.ResetColor();
                string? line = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(line)) { break; }
                ranges.Add(line);
            }
            if (ranges.Count == 0) { Console.WriteLine("No ranges given."); return; }

            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            await RunSplit(input, ranges.ToArray(), outDir, ct);
        }

        static async Task SplitEveryNPages(CancellationToken ct)
        {
            (string input, int totalPages) = await PromptInput(ct);
            if (input == null!) { return; }

            Console.Write("Chunk size (pages per output): ");
            if (!int.TryParse(Console.ReadLine(), out int chunk) || chunk < 1)
            {
                Console.WriteLine("Invalid chunk size."); return;
            }

            List<string> ranges = new();
            for (int start = 1; start <= totalPages; start += chunk)
            {
                int end = Math.Min(start + chunk - 1, totalPages);
                ranges.Add(start == end ? $"{start}" : $"{start}-{end}");
            }
            Console.WriteLine($"Will produce {ranges.Count} part(s).");

            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            await RunSplit(input, ranges.ToArray(), outDir, ct);
        }

        static async Task<(string path, int pages)> PromptInput(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("File not found.");
                return (null!, 0);
            }
            int pages;
            try
            {
                pages = await PdfInfo.GetPageCountAsync(path, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [error] {ex.Message}");
                return (null!, 0);
            }
            Console.WriteLine($"Source pages : {pages}");
            return (path, pages);
        }

        static async Task RunSplit(string input, string[] ranges, string outDir, CancellationToken ct)
        {
            var progress = new Progress<PdfSplitterProgressEventArgs>(e =>
            {
                Console.WriteLine($"  [{e.PartIndex + 1}/{e.TotalParts}] range '{e.PageRange}' -> {Path.GetFileName(e.OutputPath)}");
            });

            DateTime t0 = DateTime.UtcNow;
            try
            {
                List<string> produced = await PdfSplitter.SplitToFilesAsync(
                    input,
                    ranges,
                    outDir,
                    fileNamePrefix: Path.GetFileNameWithoutExtension(input),
                    progress: progress,
                    cancellationToken: ct);
                Console.WriteLine();
                Console.WriteLine($"Done in {(DateTime.UtcNow - t0).TotalMilliseconds:F0} ms. {produced.Count} file(s) written to {outDir}.");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("cancelled.");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
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
            Console.WriteLine("║      PDF Splitter by Page Range                  ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Split a PDF into smaller files by page ranges or fixed chunks.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / ranges   Split into custom ranges (\"1-3\", \"4,6\", \"7-9\")");
            Console.WriteLine("  2 / every    Split every N pages (auto-generates ranges)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
