using LMKit.Document.Layout;
using LMKit.Document.Pdf;
using LMKit.Document.Search;
using System.Text;

namespace pdf_text_search_with_highlights
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
                    case "1": case "search":
                        await SearchSingle(cts.Token);
                        break;
                    case "2": case "folder":
                        await SearchFolder(cts.Token);
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

        static async Task SearchSingle(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }
            Console.Write("Password (blank if none): ");
            string password = Console.ReadLine() ?? "";

            (TextSearchOptions options, int max) = PromptSearchOptions();

            while (!ct.IsCancellationRequested)
            {
                Console.WriteLine();
                Console.Write("Query (blank to return to menu): ");
                string? query = Console.ReadLine();
                if (string.IsNullOrEmpty(query)) { return; }

                PdfTextSearchResult result;
                DateTime t0 = DateTime.UtcNow;
                try
                {
                    result = await PdfSearch.FindTextAsync(input, query, pageRange: null,
                        textOptions: options, password: password, cancellationToken: ct);
                }
                catch (OperationCanceledException) { Console.WriteLine("cancelled."); return; }
                catch (Exception ex) { Console.WriteLine($"  [error] {ex.Message}"); continue; }

                double elapsed = (DateTime.UtcNow - t0).TotalMilliseconds;
                Console.WriteLine($"  Scanned {result.ScannedPages}/{result.PageCount} pages in {elapsed:F0} ms. {result.TotalMatches} match(es){(result.LimitedByMaxMatches ? " (capped)" : "")}");
                PrintMatches(result);
                MaybeExportCsv(input, query, result);
            }
        }

        static async Task SearchFolder(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of PDFs: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) { Console.WriteLine("Folder not found."); return; }
            Console.Write("Password to try on each (blank if none): ");
            string password = Console.ReadLine() ?? "";

            (TextSearchOptions options, int max) = PromptSearchOptions();

            Console.Write("Query: ");
            string? query = Console.ReadLine();
            if (string.IsNullOrEmpty(query)) { return; }

            string[] pdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pdfs.Length == 0) { Console.WriteLine("No .pdf files found."); return; }

            Console.Write("Output CSV path (blank = skip): ");
            string? csvPath = Console.ReadLine()?.Trim().Trim('"');
            StreamWriter? csv = null;
            if (!string.IsNullOrWhiteSpace(csvPath))
            {
                csv = new StreamWriter(csvPath, false, new UTF8Encoding(true));
                csv.WriteLine("source,page,top,left,bottom,right,snippet");
            }

            Console.WriteLine();
            int totalHits = 0;
            int hitsInFiles = 0;
            foreach (string p in pdfs)
            {
                PdfTextSearchResult result;
                try
                {
                    result = await PdfSearch.FindTextAsync(p, query, pageRange: null,
                        textOptions: options, password: password, cancellationToken: ct);
                }
                catch (OperationCanceledException) { Console.WriteLine("cancelled."); break; }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                    continue;
                }

                if (result.TotalMatches == 0) { continue; }
                hitsInFiles++;
                totalHits += result.TotalMatches;
                Console.WriteLine($"  {Truncate(Path.GetFileName(p), 50),-50} {result.TotalMatches,5} match(es)");

                if (csv != null)
                {
                    foreach (TextMatch m in result.Matches)
                    {
                        csv.Write(Csv(Path.GetFileName(p))); csv.Write(',');
                        csv.Write(m.PageIndex + 1); csv.Write(',');
                        csv.Write($"{m.Bounds.Top:F1}"); csv.Write(',');
                        csv.Write($"{m.Bounds.Left:F1}"); csv.Write(',');
                        csv.Write($"{m.Bounds.Bottom:F1}"); csv.Write(',');
                        csv.Write($"{m.Bounds.Right:F1}"); csv.Write(',');
                        csv.WriteLine(Csv(m.Snippet.Trim()));
                    }
                }
            }
            csv?.Dispose();

            Console.WriteLine();
            Console.WriteLine($"Found {totalHits} match(es) in {hitsInFiles} file(s) (of {pdfs.Length} scanned).");
            if (csv != null) { Console.WriteLine($"CSV: {csvPath}"); }
            Console.WriteLine();
        }

        static (TextSearchOptions, int max) PromptSearchOptions()
        {
            Console.Write("Whole word? (y/N): ");
            bool wholeWord = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");
            Console.Write("Case sensitive? (y/N): ");
            bool caseSensitive = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");
            Console.Write("Max matches per file (blank = unlimited): ");
            string? maxRaw = Console.ReadLine()?.Trim();
            int max = int.MaxValue;
            if (!string.IsNullOrWhiteSpace(maxRaw) && int.TryParse(maxRaw, out int parsed) && parsed > 0) { max = parsed; }

            return (new TextSearchOptions
            {
                Comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase,
                WholeWord = wholeWord,
                MaxResults = max,
                ContextChars = 60,
            }, max);
        }

        static void PrintMatches(PdfTextSearchResult result)
        {
            int n = 1;
            foreach (TextMatch m in result.Matches)
            {
                Console.WriteLine($"  [{n,3}] page {m.PageIndex + 1,4} | bbox ({m.Bounds.Top:F0},{m.Bounds.Left:F0})-({m.Bounds.Bottom:F0},{m.Bounds.Right:F0})");
                string ctx = m.Snippet.Trim();
                if (ctx.Length > 120) { ctx = ctx.Substring(0, 119) + "…"; }
                Console.WriteLine($"        \"{ctx}\"");
                n++;
            }
        }

        static void MaybeExportCsv(string input, string query, PdfTextSearchResult result)
        {
            if (result.TotalMatches == 0) { return; }
            Console.Write("Export hits to CSV? (path, blank = skip): ");
            string? csvPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(csvPath)) { return; }

            using StreamWriter csv = new(csvPath, false, new UTF8Encoding(true));
            csv.WriteLine("source,page,top,left,bottom,right,snippet");
            foreach (TextMatch m in result.Matches)
            {
                csv.Write(Csv(Path.GetFileName(input))); csv.Write(',');
                csv.Write(m.PageIndex + 1); csv.Write(',');
                csv.Write($"{m.Bounds.Top:F1}"); csv.Write(',');
                csv.Write($"{m.Bounds.Left:F1}"); csv.Write(',');
                csv.Write($"{m.Bounds.Bottom:F1}"); csv.Write(',');
                csv.Write($"{m.Bounds.Right:F1}"); csv.Write(',');
                csv.WriteLine(Csv(m.Snippet.Trim()));
            }
            Console.WriteLine($"  CSV: {csvPath}");
        }

        static string Csv(string s)
        {
            if (s == null) { return ""; }
            bool needsQuotes = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            string body = s.Replace("\"", "\"\"");
            return needsQuotes ? $"\"{body}\"" : body;
        }

        static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

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
            Console.WriteLine("║      PDF Text Search with Highlights             ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Find text in PDFs with page + bounding-box coordinates for downstream highlighting.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / search   Open one PDF and run repeated queries (REPL)");
            Console.WriteLine("  2 / folder   Search one term across every PDF in a folder (CSV export)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
