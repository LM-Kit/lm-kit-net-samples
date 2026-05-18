using LMKit.Data;
using LMKit.Document.Pdf;
using LMKit.Document.Search;
using LMKit.Media.Image;
using System.Text;

namespace encrypted_pdf_workflows
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
                    case "1": case "inspect":
                        await InspectEncrypted(cts.Token);
                        break;
                    case "2": case "render":
                        await RenderFirstPage(cts.Token);
                        break;
                    case "3": case "search":
                        await SearchEncrypted(cts.Token);
                        break;
                    case "4": case "extract":
                        await ExtractPages(cts.Token);
                        break;
                    case "5": case "edit":
                        await DropEveryOther(cts.Token);
                        break;
                    case "6": case "all":
                        await RunAll(cts.Token);
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

        static (string input, string password) PromptCredentials()
        {
            Console.WriteLine();
            Console.Write("Path to an encrypted PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                Console.WriteLine("File not found.");
                return (null!, null!);
            }
            Console.Write("Password: ");
            string password = Console.ReadLine() ?? "";
            return (input, password);
        }

        static async Task InspectEncrypted(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            try
            {
                int pages = await PdfInfo.GetPageCountAsync(input, password, ct);
                PdfMetadata meta = await PdfInfo.GetMetadataAsync(input, password, ct);
                int rev = await PdfInfo.GetSecurityHandlerRevisionAsync(input, password, ct);
                Console.WriteLine();
                Console.WriteLine($"  Pages           : {pages}");
                Console.WriteLine($"  Title           : {meta.Title ?? "(none)"}");
                Console.WriteLine($"  Author          : {meta.Author ?? "(none)"}");
                Console.WriteLine($"  File version    : 1.{meta.FileVersion}");
                Console.WriteLine($"  Security handler: revision {rev}");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message} (wrong password? unsupported encryption?)");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task RenderFirstPage(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            Console.Write("Output PNG path: ");
            string? outPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outPath)) { Console.WriteLine("Output path required."); return; }

            try
            {
                await PdfRenderer.SavePageAsPngAsync(input, 0, outPath, new PdfRenderOptions
                {
                    Password = password,
                    Zoom = 2.0,
                    PixelFormat = ImagePixelFormat.RGB24,
                }, cancellationToken: ct);
                Console.WriteLine($"  wrote {outPath} ({new FileInfo(outPath).Length / 1024:N0} KB)");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task SearchEncrypted(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            Console.Write("Search query: ");
            string? query = Console.ReadLine();
            if (string.IsNullOrEmpty(query)) { return; }

            try
            {
                PdfTextSearchResult result = await PdfSearch.FindTextAsync(input, query, password: password, cancellationToken: ct);
                Console.WriteLine($"  '{query}' -> {result.TotalMatches} match(es) across {result.ScannedPages}/{result.PageCount} page(s)");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task ExtractPages(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            Console.Write("Page range (e.g. \"1-2\" or \"1,3,5\"): ");
            string? range = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(range)) { return; }
            Console.Write("Output PDF path: ");
            string? outPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outPath)) { Console.WriteLine("Output path required."); return; }

            try
            {
                await PdfSplitter.ExtractPagesAsync(input, range, outPath, password: password, cancellationToken: ct);
                int pages = await PdfInfo.GetPageCountAsync(outPath, cancellationToken: ct);
                Console.WriteLine($"  wrote {outPath} ({pages} pages)");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task DropEveryOther(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            Console.Write("Output PDF path: ");
            string? outPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outPath)) { Console.WriteLine("Output path required."); return; }

            try
            {
                using Attachment src = new(input, Path.GetFileName(input), password);
                int total = await PdfInfo.GetPageCountAsync(src, ct);
                PageEdit[] keepEven = Enumerable.Range(0, total).Where(i => i % 2 == 0).Select(i => new PageEdit(i)).ToArray();
                await PdfEditor.ApplyToFileAsync(src, keepEven, outPath, ct);
                int pages = await PdfInfo.GetPageCountAsync(outPath, cancellationToken: ct);
                Console.WriteLine($"  wrote {outPath} ({pages} pages, kept even-indexed pages)");
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
            }
            Console.WriteLine();
        }

        static async Task RunAll(CancellationToken ct)
        {
            (string input, string password) = PromptCredentials();
            if (input == null!) { return; }
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);
            Console.Write("Search query for step 3 (default 'the'): ");
            string query = Console.ReadLine()?.Trim() ?? "";
            if (string.IsNullOrEmpty(query)) { query = "the"; }

            try
            {
                Console.WriteLine();
                Console.WriteLine("--- 1. Inspect metadata ---");
                int pages = await PdfInfo.GetPageCountAsync(input, password, ct);
                PdfMetadata meta = await PdfInfo.GetMetadataAsync(input, password, ct);
                int rev = await PdfInfo.GetSecurityHandlerRevisionAsync(input, password, ct);
                Console.WriteLine($"  pages={pages}  title={meta.Title ?? "(none)"}  security-rev={rev}");

                Console.WriteLine();
                Console.WriteLine("--- 2. Render first page ---");
                string png = Path.Combine(outDir, "page-1.png");
                await PdfRenderer.SavePageAsPngAsync(input, 0, png, new PdfRenderOptions
                {
                    Password = password, Zoom = 2.0, PixelFormat = ImagePixelFormat.RGB24,
                }, cancellationToken: ct);
                Console.WriteLine($"  wrote {png}");

                Console.WriteLine();
                Console.WriteLine($"--- 3. Search '{query}' ---");
                PdfTextSearchResult result = await PdfSearch.FindTextAsync(input, query, password: password, cancellationToken: ct);
                Console.WriteLine($"  {result.TotalMatches} match(es) across {result.ScannedPages} page(s)");

                Console.WriteLine();
                Console.WriteLine("--- 4. Extract pages 1-2 ---");
                string firstTwo = Path.Combine(outDir, "first-two-pages.pdf");
                await PdfSplitter.ExtractPagesAsync(input, "1-2", firstTwo, password: password, cancellationToken: ct);
                Console.WriteLine($"  wrote {firstTwo}");

                Console.WriteLine();
                Console.WriteLine("--- 5. Keep every-other page ---");
                using Attachment src = new(input, Path.GetFileName(input), password);
                PageEdit[] keepEven = Enumerable.Range(0, pages).Where(i => i % 2 == 0).Select(i => new PageEdit(i)).ToArray();
                string edited = Path.Combine(outDir, "every-other-page.pdf");
                await PdfEditor.ApplyToFileAsync(src, keepEven, edited, ct);
                Console.WriteLine($"  wrote {edited}");

                Console.WriteLine();
                Console.WriteLine($"All outputs in: {Path.GetFullPath(outDir)}");
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
            Console.WriteLine("║      Encrypted PDF Workflows                     ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Inspect, render, search, split, and edit password-protected PDFs.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / inspect   Read metadata and security handler revision");
            Console.WriteLine("  2 / render    Render first page to PNG");
            Console.WriteLine("  3 / search    Run a layout-aware text search");
            Console.WriteLine("  4 / extract   Extract a page range to a new PDF");
            Console.WriteLine("  5 / edit      Keep every-other page via PdfEditor");
            Console.WriteLine("  6 / all       Run all 5 steps end-to-end");
            Console.WriteLine("  q / quit      Exit");
            Console.WriteLine();
        }
    }
}
