using LMKit.Document.Pdf;
using System.Text;

namespace pdf_merger_batch
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
                    case "1": case "list":
                        await MergeFromList(cts.Token);
                        break;
                    case "2": case "folder":
                        await MergeFromFolder(cts.Token);
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

        static async Task MergeFromList(CancellationToken ct)
        {
            Console.WriteLine();
            Console.WriteLine("Paste PDF paths, one per line. Blank line ends the list.");
            List<string> inputs = new();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"  [{inputs.Count + 1}] ");
                Console.ResetColor();
                string? line = Console.ReadLine()?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(line)) { break; }
                if (!File.Exists(line))
                {
                    Console.WriteLine($"  (not found, skipped) {line}");
                    continue;
                }
                inputs.Add(line);
            }
            if (inputs.Count < 2)
            {
                Console.WriteLine("Need at least two files to merge.");
                return;
            }

            Console.Write("Output path (e.g. merged.pdf): ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine("Output path required."); return; }

            await Merge(inputs, output, ct);
        }

        static async Task MergeFromFolder(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to folder containing PDFs: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine("Folder not found.");
                return;
            }
            Console.Write("Recurse into subfolders? (y/N): ");
            bool recurse = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");
            Console.Write("Sort order [name|mtime] (default name): ");
            string sort = (Console.ReadLine()?.Trim().ToLowerInvariant() ?? "name");

            SearchOption opt = recurse ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            IEnumerable<string> q = Directory.EnumerateFiles(dir, "*.pdf", opt);
            List<string> inputs = (sort == "mtime"
                ? q.OrderBy(f => File.GetLastWriteTimeUtc(f))
                : q.OrderBy(f => f, StringComparer.OrdinalIgnoreCase)).ToList();

            if (inputs.Count < 2) { Console.WriteLine("Need at least two PDFs in the folder."); return; }

            Console.WriteLine($"Found {inputs.Count} PDF(s).");
            Console.Write("Output path (e.g. merged.pdf): ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine("Output path required."); return; }

            await Merge(inputs, output, ct);
        }

        static async Task Merge(List<string> inputs, string output, CancellationToken ct)
        {
            int totalPages = 0;
            Console.WriteLine();
            foreach (string p in inputs)
            {
                try
                {
                    int pages = await PdfInfo.GetPageCountAsync(p, cancellationToken: ct);
                    totalPages += pages;
                    Console.WriteLine($"  + {Truncate(Path.GetFileName(p), 50),-50}  {pages,5} pages");
                }
                catch (OperationCanceledException) { Console.WriteLine("cancelled."); return; }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [skip] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                    inputs.Remove(p);
                }
            }
            if (inputs.Count < 2) { Console.WriteLine("Not enough valid inputs."); return; }

            Console.WriteLine();
            Console.WriteLine($"Total inputs : {inputs.Count}");
            Console.WriteLine($"Total pages  : {totalPages}");
            Console.WriteLine($"Output       : {output}");
            Console.Write("Merging... ");
            DateTime t0 = DateTime.UtcNow;
            try
            {
                await PdfMerger.MergeFilesAsync(inputs.ToArray(), output, ct);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("cancelled.");
                return;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  [error] {ex.Message}");
                Console.ResetColor();
                return;
            }
            FileInfo fi = new(output);
            Console.WriteLine($"done in {(DateTime.UtcNow - t0).TotalMilliseconds:F0} ms. Output size: {fi.Length / 1024:N0} KB");
            Console.WriteLine();
        }

        static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

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
            Console.WriteLine("║      PDF Merger Batch                            ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Combine multiple PDFs into a single packet.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / list     Merge a typed list of PDF paths");
            Console.WriteLine("  2 / folder   Merge every PDF in a folder (sorted)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
