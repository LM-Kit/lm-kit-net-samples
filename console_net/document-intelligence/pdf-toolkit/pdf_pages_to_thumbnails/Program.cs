using LMKit.Document.Pdf;
using LMKit.Media.Image;
using System.Diagnostics;
using System.Text;

namespace pdf_pages_to_thumbnails
{
    internal class Program
    {
        enum Format { Png, Jpeg, Webp, Bmp, Tiff, Tga, Pnm }

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
                    case "1": case "thumbs":
                        await Render(1.0, Format.Jpeg, "Quick thumbnails (1x ~72 ppi, JPEG q=80)", cts.Token, jpegQuality: 80);
                        break;
                    case "2": case "preview":
                        await Render(2.0, Format.Png, "Page previews (2x ~144 ppi, PNG)", cts.Token);
                        break;
                    case "3": case "archival":
                        await Render(4.0, Format.Tiff, "Archival (4x ~288 ppi, TIFF)", cts.Token);
                        break;
                    case "4": case "custom":
                        await RenderCustom(cts.Token);
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

        static async Task Render(double zoom, Format format, string label, CancellationToken token,
            int pngLevel = 6, int jpegQuality = 88, int webpQuality = 85, bool grayscale = false, string? pageRange = null)
        {
            Console.WriteLine();
            Console.WriteLine(label);
            Console.Write("Path to a PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            await Run(input, outDir, new PdfRenderOptions
            {
                Zoom = zoom,
                PixelFormat = grayscale ? ImagePixelFormat.GRAY8 : ImagePixelFormat.RGB24,
                PageRange = pageRange,
            }, format, pngLevel, jpegQuality, webpQuality, bmpRle: false, tgaRle: false, token);
        }

        static async Task RenderCustom(CancellationToken token)
        {
            Console.WriteLine();
            Console.Write("Path to a PDF: ");
            string? input = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(input) || !File.Exists(input)) { Console.WriteLine("File not found."); return; }
            Console.Write("Output directory: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { Console.WriteLine("Output directory required."); return; }
            Directory.CreateDirectory(outDir);

            Console.Write("Zoom (1.0 = 72 ppi, 2.0 = 144 ppi, ...): ");
            if (!double.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double zoom) || zoom <= 0)
            {
                zoom = 2.0;
            }
            Console.Write("Format [png|jpeg|webp|bmp|tiff|tga|pnm] (default png): ");
            string fmt = (Console.ReadLine()?.Trim().ToLowerInvariant() ?? "png");
            Format format = fmt switch
            {
                "jpg" or "jpeg" => Format.Jpeg,
                "webp" => Format.Webp,
                "bmp" => Format.Bmp,
                "tif" or "tiff" => Format.Tiff,
                "tga" => Format.Tga,
                "pnm" or "ppm" or "pgm" => Format.Pnm,
                _ => Format.Png,
            };
            Console.Write("Grayscale? (y/N): ");
            bool grayscale = (Console.ReadLine()?.Trim().ToLowerInvariant() == "y");
            Console.Write("Page range (blank = all, e.g. \"1-3,5\"): ");
            string? range = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(range)) { range = null; }

            await Run(input, outDir, new PdfRenderOptions
            {
                Zoom = zoom,
                PixelFormat = grayscale ? ImagePixelFormat.GRAY8 : ImagePixelFormat.RGB24,
                PageRange = range,
            }, format, pngLevel: 6, jpegQuality: 88, webpQuality: 85, bmpRle: false, tgaRle: false, token);
        }

        static async Task Run(string input, string outDir, PdfRenderOptions options,
            Format format, int pngLevel, int jpegQuality, int webpQuality, bool bmpRle, bool tgaRle, CancellationToken ct)
        {
            int sourcePages;
            try { sourcePages = await PdfInfo.GetPageCountAsync(input, cancellationToken: ct); }
            catch (Exception ex) { Console.WriteLine($"  [error] {ex.Message}"); return; }

            string prefix = Path.GetFileNameWithoutExtension(input);
            Console.WriteLine();
            Console.WriteLine($"Source pages : {sourcePages}");
            Console.WriteLine($"Format       : {format}  Zoom: {options.Zoom}x ({options.Zoom * 72:F0} ppi)");
            Console.WriteLine($"Page range   : {options.PageRange ?? "(all)"}");
            Console.WriteLine();

            Stopwatch sw = Stopwatch.StartNew();
            int produced = 0;
            long totalBytes = 0;
            try
            {
                await foreach (var (pageIndex, img) in PdfRenderer.RenderPagesAsync(input, options, ct))
                {
                    int pageNum = pageIndex + 1;
                    int width = sourcePages < 10 ? 1 : sourcePages < 100 ? 2 : sourcePages < 1000 ? 3 : 4;
                    string outPath = Path.Combine(outDir, $"{prefix}-page-{pageNum.ToString().PadLeft(width, '0')}{ExtensionFor(format)}");
                    using (img)
                    {
                        bool ok = format switch
                        {
                            Format.Png => img.SaveAsPng(outPath, pngLevel),
                            Format.Jpeg => img.SaveAsJpeg(outPath, jpegQuality),
                            Format.Webp => img.SaveAsWebp(outPath, webpQuality),
                            Format.Bmp => img.SaveAsBmp(outPath, bmpRle),
                            Format.Tiff => img.SaveAsTiff(outPath),
                            Format.Tga => img.SaveAsTga(outPath, tgaRle),
                            Format.Pnm => img.SaveAsPnm(outPath),
                            _ => false,
                        };
                        if (!ok) { Console.WriteLine($"  [save failed] {outPath}"); continue; }
                        long sz = new FileInfo(outPath).Length;
                        totalBytes += sz;
                        produced++;
                        Console.WriteLine($"  {Path.GetFileName(outPath),-40}  {img.Width}x{img.Height}  {sz / 1024,6:N0} KB");
                    }
                }
            }
            catch (OperationCanceledException) { Console.WriteLine("cancelled."); return; }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  [error] {ex.Message}");
                Console.ResetColor();
                return;
            }
            sw.Stop();
            Console.WriteLine();
            Console.WriteLine($"Wrote {produced} file(s), {totalBytes / 1024:N0} KB total, in {sw.ElapsedMilliseconds} ms.");
            Console.WriteLine();
        }

        static string ExtensionFor(Format f) => f switch
        {
            Format.Png => ".png",
            Format.Jpeg => ".jpg",
            Format.Webp => ".webp",
            Format.Bmp => ".bmp",
            Format.Tiff => ".tif",
            Format.Tga => ".tga",
            Format.Pnm => ".pnm",
            _ => ".bin",
        };

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
            Console.WriteLine("║      PDF Pages to Image Thumbnails               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Render PDF pages to image files for previews, OCR pre-processing, or archives.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / thumbs    Quick thumbnails (1x ~72 ppi, JPEG q=80)");
            Console.WriteLine("  2 / preview   Page previews (2x ~144 ppi, PNG)");
            Console.WriteLine("  3 / archival  Archival (4x ~288 ppi, TIFF)");
            Console.WriteLine("  4 / custom    Custom zoom / format / range");
            Console.WriteLine("  q / quit      Exit");
            Console.WriteLine();
        }
    }
}
