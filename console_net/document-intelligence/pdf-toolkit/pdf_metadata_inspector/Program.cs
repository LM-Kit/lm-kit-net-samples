using LMKit.Document.Pdf;
using System.Text;

namespace pdf_metadata_inspector
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
                        await InspectSingleFile(cts.Token);
                        break;
                    case "2": case "folder":
                        await InspectFolder(cts.Token);
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

        static async Task InspectSingleFile(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                Console.WriteLine("File not found.");
                return;
            }
            Console.Write("Password (blank if none): ");
            string password = Console.ReadLine() ?? "";

            try
            {
                PdfMetadata m = await PdfInfo.GetMetadataAsync(path, password, ct);
                Console.WriteLine();
                Console.WriteLine($"  Title         : {Show(m.Title)}");
                Console.WriteLine($"  Author        : {Show(m.Author)}");
                Console.WriteLine($"  Subject       : {Show(m.Subject)}");
                Console.WriteLine($"  Keywords      : {Show(m.Keywords)}");
                Console.WriteLine($"  Creator       : {Show(m.Creator)}");
                Console.WriteLine($"  Producer      : {Show(m.Producer)}");
                Console.WriteLine($"  Creation date : {Show(m.CreationDate)}");
                Console.WriteLine($"  Mod date      : {Show(m.ModDate)}");
                Console.WriteLine($"  Pages         : {m.PageCount}");
                Console.WriteLine($"  PDF version   : 1.{m.FileVersion}");
                Console.WriteLine($"  XMP present   : {!string.IsNullOrEmpty(m.XmpMetadata)} ({m.XmpMetadata?.Length ?? 0} chars)");

                int sec = await PdfInfo.GetSecurityHandlerRevisionAsync(path, password, ct);
                DocumentPermissions perms = await PdfInfo.GetPermissionsAsync(path, password, ct);
                Console.WriteLine();
                Console.WriteLine($"  Encrypted     : {sec > 0} (security handler revision = {sec})");
                Console.WriteLine($"  Permissions   : {perms}");

                Console.WriteLine();
                int pageSample = Math.Min(10, m.PageCount);
                for (int i = 0; i < pageSample; i++)
                {
                    PdfPageInfo p = await PdfInfo.GetPageInfoAsync(path, i, password, ct);
                    Console.WriteLine(
                        $"  p.{i + 1,3}  {p.Width,7:F1} x {p.Height,7:F1} pt   " +
                        $"orientation={p.Orientation,-12}  text-only={p.IsTextOnly}");
                }
                if (m.PageCount > pageSample)
                {
                    Console.WriteLine($"  ...  ({m.PageCount - pageSample} more)");
                }
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

        static async Task InspectFolder(CancellationToken ct)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of PDFs: ");
            string? dir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir))
            {
                Console.WriteLine("Folder not found.");
                return;
            }
            Console.Write("Password to try on each (blank if none): ");
            string password = Console.ReadLine() ?? "";

            string[] pdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.AllDirectories)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (pdfs.Length == 0) { Console.WriteLine("No .pdf files found."); return; }

            Console.WriteLine();
            Console.WriteLine($"  {"File",-50} {"Pages",6} {"v1.",4} {"Encrypted",-10} Title");
            foreach (string p in pdfs)
            {
                try
                {
                    PdfMetadata m = await PdfInfo.GetMetadataAsync(p, password, ct);
                    int sec = await PdfInfo.GetSecurityHandlerRevisionAsync(p, password, ct);
                    Console.WriteLine(
                        $"  {Truncate(Path.GetFileName(p), 50),-50} {m.PageCount,6} {m.FileVersion,4} " +
                        $"{(sec > 0 ? "yes" : "no"),-10} {Truncate(m.Title ?? "", 40)}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  [error] {Path.GetFileName(p)}: {ex.Message}");
                    Console.ResetColor();
                }
            }
            Console.WriteLine();
        }

        static string Show(string? s) =>
            string.IsNullOrWhiteSpace(s) ? "(empty)" : s.Length > 80 ? s.Substring(0, 79) + "…" : s;

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
            Console.WriteLine("║      PDF Metadata Inspector                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Inspect metadata, encryption status, permissions, and per-page info.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / file     Inspect a single PDF (full metadata + page table)");
            Console.WriteLine("  2 / folder   Inspect every PDF in a folder (compact one-line view)");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }
    }
}
