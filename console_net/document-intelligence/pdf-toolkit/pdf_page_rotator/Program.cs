using LMKit.Data;
using LMKit.Document.Pdf;
using System.Text;

namespace pdf_page_rotator
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
                    case "1": case "auto":
                        await AutoFix(cts.Token);
                        break;
                    case "2": case "all":
                        await RotateAll(cts.Token);
                        break;
                    case "3": case "pages":
                        await RotateSelected(cts.Token);
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

        static async Task AutoFix(CancellationToken ct)
        {
            (string input, int pages, string output) = await PromptInputOutput(ct);
            if (input == null!) { return; }

            List<PageEdit> edits = new();
            Console.WriteLine("Scanning page orientations...");
            for (int i = 0; i < pages; i++)
            {
                PdfPageInfo pi = await PdfInfo.GetPageInfoAsync(input, i, cancellationToken: ct);
                if (pi.Orientation != PageOrientations.Normal)
                {
                    Console.WriteLine($"  p.{i + 1,3}  {pi.Orientation} -> Normal");
                    edits.Add(new PageEdit(i, PageOrientations.Normal));
                }
            }
            if (edits.Count == 0)
            {
                Console.WriteLine("All pages already Normal. No changes needed.");
                return;
            }
            await Apply(input, output, edits, ct);
        }

        static async Task RotateAll(CancellationToken ct)
        {
            (string input, int pages, string output) = await PromptInputOutput(ct);
            if (input == null!) { return; }

            PageOrientations? target = PromptRotation();
            if (target == null) { return; }

            List<PageEdit> edits = new(pages);
            for (int i = 0; i < pages; i++) { edits.Add(new PageEdit(i, target.Value)); }
            await Apply(input, output, edits, ct);
        }

        static async Task RotateSelected(CancellationToken ct)
        {
            (string input, int pages, string output) = await PromptInputOutput(ct);
            if (input == null!) { return; }

            PageOrientations? target = PromptRotation();
            if (target == null) { return; }

            Console.Write($"Page numbers to rotate (1-based, e.g. \"1,3-5,8\"): ");
            string? spec = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(spec)) { Console.WriteLine("No pages given."); return; }

            HashSet<int> indices = ParsePageSpec(spec, pages);
            if (indices.Count == 0) { Console.WriteLine("No valid page numbers."); return; }

            List<PageEdit> edits = indices.OrderBy(i => i).Select(i => new PageEdit(i, target.Value)).ToList();
            Console.WriteLine($"Will rotate {edits.Count} page(s).");
            await Apply(input, output, edits, ct);
        }

        static PageOrientations? PromptRotation()
        {
            Console.Write("Rotation [90|180|270]: ");
            if (!int.TryParse(Console.ReadLine(), out int deg))
            {
                Console.WriteLine("Invalid rotation.");
                return null;
            }
            return deg switch
            {
                90 => PageOrientations.Rotated90CW,
                180 => PageOrientations.Rotated180,
                270 => PageOrientations.Rotated90CCW,
                _ => null,
            };
        }

        static HashSet<int> ParsePageSpec(string spec, int totalPages)
        {
            HashSet<int> set = new();
            foreach (string tok in spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                int dash = tok.IndexOf('-');
                if (dash > 0)
                {
                    if (int.TryParse(tok.AsSpan(0, dash), out int a) && int.TryParse(tok.AsSpan(dash + 1), out int b))
                    {
                        if (a > b) (a, b) = (b, a);
                        for (int i = a; i <= b; i++) { if (i >= 1 && i <= totalPages) { set.Add(i - 1); } }
                    }
                }
                else if (int.TryParse(tok, out int n) && n >= 1 && n <= totalPages)
                {
                    set.Add(n - 1);
                }
            }
            return set;
        }

        static async Task<(string input, int pages, string output)> PromptInputOutput(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input))
            {
                Console.WriteLine("File not found.");
                return (null!, 0, null!);
            }
            int pages;
            try { pages = await PdfInfo.GetPageCountAsync(input, cancellationToken: ct); }
            catch (Exception ex) { Console.WriteLine($"  [error] {ex.Message}"); return (null!, 0, null!); }
            Console.WriteLine($"Source pages : {pages}");

            Console.Write("Output PDF path: ");
            string? output = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(output)) { Console.WriteLine("Output path required."); return (null!, 0, null!); }
            return (input, pages, output);
        }

        static async Task Apply(string input, string output, List<PageEdit> edits, CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write($"Writing {output} with {edits.Count} edit(s)... ");
            DateTime t0 = DateTime.UtcNow;
            try
            {
                using Attachment source = new(input);
                await PdfEditor.ApplyToFileAsync(source, edits, output, ct);
                Console.WriteLine($"done in {(DateTime.UtcNow - t0).TotalMilliseconds:F0} ms.");
            }
            catch (OperationCanceledException) { Console.WriteLine("cancelled."); }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  [error] {ex.Message}");
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
            Console.WriteLine("║      PDF Page Rotator                            ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Fix sideways scans or rotate selected pages with one call.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / auto     Auto-fix: rotate every non-Normal page back to Normal");
            Console.WriteLine("  2 / all      Rotate every page by 90, 180, or 270 degrees");
            Console.WriteLine("  3 / pages    Rotate selected page numbers (\"1,3-5,8\")");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
