using LMKit.Data;
using LMKit.Document.Conversion;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace document_to_markdown
{
    /// <summary>
    /// End-to-end demonstration of <see cref="DocumentToMarkdown"/>, LM-Kit.NET's
    /// state-of-the-art universal document-to-Markdown conversion engine.
    ///
    /// The demo highlights:
    ///   * Universal format coverage: PDF, DOCX, PPTX, XLSX, EML, MBOX, HTML, TXT, and any image format.
    ///   * Three switchable strategies: TextExtraction, VlmOcr, Hybrid (recommended default).
    ///   * Format-aware specialized converters (EML / MBOX / HTML / DOCX) for structurally rich output.
    ///   * Per-page live progress with PageStarting / PageCompleted events.
    ///   * Per-page telemetry: elapsed time, strategy, token count, quality score.
    ///   * Optional YAML front matter, page separators, page-range selection.
    ///   * Direct-to-disk conversion via ConvertToFile.
    ///   * Traditional OCR fallback (LMKitOcr) for the TextExtraction strategy on images.
    /// </summary>
    internal class Program
    {
        private static bool _isDownloading;

        private static void Main(string[] args)
        {
            // A free community license is available at: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintBanner();

            //------------------------------------------------------------
            // 1. Pick the conversion strategy
            //------------------------------------------------------------
            DocumentToMarkdownStrategy strategy = PromptStrategy();

            //------------------------------------------------------------
            // 2. Load a vision-language model (only if vision may be needed)
            //------------------------------------------------------------
            LM? visionModel = null;
            if (strategy != DocumentToMarkdownStrategy.TextExtraction)
            {
                visionModel = PromptModel();
                Console.Clear();
                PrintBanner();
            }

            //------------------------------------------------------------
            // 3. Build the converter and subscribe to progress events
            //------------------------------------------------------------
            DocumentToMarkdown converter = visionModel != null
                ? new DocumentToMarkdown(visionModel)
                : new DocumentToMarkdown();

            AttachProgressHandlers(converter);

            //------------------------------------------------------------
            // 4. Conversion loop
            //------------------------------------------------------------
            PrintSection("Ready");
            Console.WriteLine("Accepted formats: PDF, DOCX, PPTX, XLSX, EML, MBOX, HTML, TXT, images (PNG/JPG/TIFF/BMP/WEBP).");
            Console.WriteLine("For image inputs the TextExtraction strategy can be paired with the built-in LMKitOcr engine.");
            Console.WriteLine();

            while (true)
            {
                string? path = PromptPath();
                if (path == null) break;

                string? pageRange = PromptPageRange();
                string? outputPath = PromptOutputPath(path);

                var options = new DocumentToMarkdownOptions
                {
                    Strategy = strategy,
                    PageRange = pageRange,
                    IncludePageSeparators = true,
                    EmitFrontMatter = true,
                    PreferMarkdownTablesForNonNested = true,
                    NormalizeWhitespace = true
                };

                // TextExtraction on pure images requires a traditional OCR engine to recover text.
                bool attachOcrEngine = strategy == DocumentToMarkdownStrategy.TextExtraction
                                       && LooksLikeImage(path);
                LMKitOcr? lmkitOcr = null;
                if (attachOcrEngine)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("  Image input detected with TextExtraction strategy. Wiring LMKitOcr for this run.");
                    Console.ResetColor();
                    lmkitOcr = new LMKitOcr();
                    options.OcrEngine = lmkitOcr;
                }

                try
                {
                    var totalSw = Stopwatch.StartNew();

                    DocumentToMarkdownResult result = outputPath == null
                        ? converter.Convert(path, options)
                        : converter.ConvertToFile(path, outputPath, options);

                    totalSw.Stop();

                    PrintSection("Markdown output");
                    if (outputPath != null)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"Saved to: {Path.GetFullPath(outputPath)}");
                        Console.ResetColor();

                        const int previewChars = 1200;
                        string md = result.Markdown ?? string.Empty;
                        string preview = md.Length > previewChars ? md.Substring(0, previewChars) + "\n…" : md;
                        Console.WriteLine();
                        Console.WriteLine("--- Preview (first 1200 chars) ---");
                        Console.WriteLine(preview);
                        Console.WriteLine("--- End preview ---");
                    }
                    else
                    {
                        Console.WriteLine(result.Markdown);
                    }

                    PrintOverallStats(result, totalSw.Elapsed);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nConversion failed: {ex.Message}");
                    Console.ResetColor();
                }
                finally
                {
                    lmkitOcr?.Dispose();
                }

                Console.WriteLine();
                Console.Write("Press Enter to convert another document, or type 'q' to quit: ");
                string again = Console.ReadLine()?.Trim() ?? string.Empty;
                if (string.Equals(again, "q", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                Console.Clear();
                PrintBanner();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"Active strategy: {strategy}   {(visionModel != null ? $"Vision model: {visionModel.Name}" : "No vision model loaded.")}");
                Console.ResetColor();
                Console.WriteLine();
            }

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        //------------------------------------------------------------
        // UI helpers
        //------------------------------------------------------------
        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔════════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║             LM-Kit.NET  Document-to-Markdown Engine                ║");
            Console.WriteLine("║   PDF · DOCX · PPTX · XLSX · EML · MBOX · HTML · TXT · Images      ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintSection(string title)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"── {title} ──────────────────────────────────────────────");
            Console.ResetColor();
        }

        private static DocumentToMarkdownStrategy PromptStrategy()
        {
            PrintSection("Step 1. Conversion strategy");
            Console.WriteLine("  0 - Hybrid          (recommended: per-page choice, fastest overall)");
            Console.WriteLine("  1 - TextExtraction  (embedded text layer only, no model required)");
            Console.WriteLine("  2 - VlmOcr          (rasterize + VLM, best for scans / images / layout)");
            Console.Write("\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            return input switch
            {
                "1" => DocumentToMarkdownStrategy.TextExtraction,
                "2" => DocumentToMarkdownStrategy.VlmOcr,
                _ => DocumentToMarkdownStrategy.Hybrid
            };
        }

        private static LM PromptModel()
        {
            PrintSection("Step 2. Vision-language model");
            Console.WriteLine("  0 - LightOn LightOnOCR 2 1B    (~2 GB VRAM)   ★ default, OCR-specialist");
            Console.WriteLine("  1 - Z.ai GLM-OCR 0.9B          (~1 GB VRAM)   lightweight OCR-specialist");
            Console.WriteLine("  2 - Z.ai GLM-V 4.6 Flash 10B   (~7 GB VRAM)   highest fidelity on complex layouts");
            Console.WriteLine("  3 - MiniCPM o 4.5 9B           (~5.9 GB VRAM)");
            Console.WriteLine("  4 - Alibaba Qwen 3.5 2B        (~2 GB VRAM)");
            Console.WriteLine("  5 - Alibaba Qwen 3.5 4B        (~3.5 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.5 9B        (~7 GB VRAM)");
            Console.WriteLine("  7 - Google Gemma 4 E4B         (~6 GB VRAM)");
            Console.WriteLine("  8 - Alibaba Qwen 3.5 27B       (~18 GB VRAM)");
            Console.WriteLine("  9 - Mistral Ministral 3 8B     (~6.5 GB VRAM)");
            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";

            string? modelId = input switch
            {
                "0" => "lightonocr-2:1b",
                "1" => "glm-ocr",
                "2" => "glm-4.6v-flash",
                "3" => "minicpm-o-45",
                "4" => "qwen3.5:2b",
                "5" => "qwen3.5:4b",
                "6" => "qwen3.5:9b",
                "7" => "gemma4:e4b",
                "8" => "qwen3.5:27b",
                "9" => "ministral3:8b",
                _ => null
            };

            LM model = modelId != null
                ? LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress)
                : new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            Console.WriteLine();
            return model;
        }

        private static string? PromptPath()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Input document");
            Console.ResetColor();
            Console.Write(" (or 'q' to quit)\n> ");

            while (true)
            {
                string raw = Console.ReadLine()?.Trim().Trim('"') ?? string.Empty;
                if (string.Equals(raw, "q", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }
                if (File.Exists(raw))
                {
                    return raw;
                }
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write("  File not found. Try again (or 'q'):\n> ");
                Console.ResetColor();
            }
        }

        private static string? PromptPageRange()
        {
            Console.Write("Page range (e.g. 1-5,7. Press Enter for all pages): ");
            string raw = Console.ReadLine()?.Trim() ?? string.Empty;
            return string.IsNullOrWhiteSpace(raw) ? null : raw;
        }

        private static string? PromptOutputPath(string inputPath)
        {
            Console.Write("Output .md path (Enter to print to console only): ");
            string raw = Console.ReadLine()?.Trim().Trim('"') ?? string.Empty;
            if (string.IsNullOrEmpty(raw))
            {
                return null;
            }

            if (Directory.Exists(raw))
            {
                string name = Path.GetFileNameWithoutExtension(inputPath) + ".md";
                return Path.Combine(raw, name);
            }

            return raw;
        }

        private static void AttachProgressHandlers(DocumentToMarkdown converter)
        {
            converter.PageStarting += (_, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                Console.WriteLine($"  ▶ Page {e.PageNumber}/{e.PageCount}  planned strategy: {e.PlannedStrategy}");
                Console.ResetColor();
            };

            converter.PageCompleted += (_, e) =>
            {
                if (e.Exception != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  ✗ Page {e.PageNumber}/{e.PageCount} failed: {e.Exception.Message}");
                    Console.ResetColor();
                    return;
                }

                var page = e.PageResult;
                if (page == null) return;

                Console.ForegroundColor = ConsoleColor.DarkGray;
                string quality = page.QualityScore.HasValue
                    ? $", quality={page.QualityScore.Value:F2}"
                    : string.Empty;
                string tokens = page.GeneratedTokenCount > 0
                    ? $", {page.GeneratedTokenCount} tok"
                    : string.Empty;
                Console.WriteLine(
                    $"  ✓ Page {page.PageNumber} done in {page.Elapsed.TotalMilliseconds:F0} ms " +
                    $"[{page.StrategyUsed}{tokens}{quality}]");
                if (!string.IsNullOrEmpty(page.Warning))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"    ⚠ {page.Warning}");
                }
                Console.ResetColor();
            };
        }

        private static void PrintOverallStats(DocumentToMarkdownResult result, TimeSpan totalElapsed)
        {
            int totalTokens = 0;
            int vlmPages = 0;
            int textPages = 0;
            foreach (var page in result.Pages)
            {
                totalTokens += page.GeneratedTokenCount;
                if (page.StrategyUsed == DocumentToMarkdownStrategy.VlmOcr) vlmPages++;
                else textPages++;
            }

            int chars = result.Markdown?.Length ?? 0;

            PrintSection("Conversion summary");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"  source             : {result.SourceName}");
            Console.WriteLine($"  requested strategy : {result.RequestedStrategy}");
            Console.WriteLine($"  effective strategy : {result.EffectiveStrategy}");
            Console.WriteLine($"  pages              : {result.Pages.Count}   (text={textPages}, vlm={vlmPages})");
            Console.WriteLine($"  total tokens (vlm) : {totalTokens}");
            Console.WriteLine($"  markdown length    : {chars:N0} chars");
            Console.WriteLine($"  elapsed (engine)   : {result.Elapsed.TotalSeconds:F2} s");
            Console.WriteLine($"  elapsed (wall)     : {totalElapsed.TotalSeconds:F2} s");
            Console.ResetColor();
        }

        private static bool LooksLikeImage(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".bmp" or ".tiff" or ".tif" or ".webp" or ".gif";
        }

        //------------------------------------------------------------
        // Model loading progress
        //------------------------------------------------------------
        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double percent = (double)bytesRead / contentLength.Value * 100;
                Console.Write($"\r  Downloading: {percent:F1}%   ");
            }
            else
            {
                Console.Write($"\r  Downloading: {bytesRead / 1024.0 / 1024.0:F1} MB   ");
            }
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\r  Loading: {progress * 100:F0}%   ");
            return true;
        }
    }
}
