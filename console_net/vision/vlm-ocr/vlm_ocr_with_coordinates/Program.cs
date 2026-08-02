using LMKit.Data;
using LMKit.Document.Conversion;
using LMKit.Document.Layout;
using LMKit.Extraction.Ocr;
using LMKit.Graphics.Drawing;
using LMKit.Graphics.Geometry;
using LMKit.Graphics.Primitives;
using LMKit.Media.Image;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace vlm_ocr_with_coordinates
{
    internal class Program
    {
        private static bool _isDownloading;

        // Models that support coordinate output.
        // This list will grow as more engines add bounding-box capabilities.
        private static readonly (string ModelId, string Label)[] SupportedModels =
        [
            ("paddleocr-vl-1.6:0.9b", "PaddlePaddle PaddleOCR VL 1.6 0.9B  (~1 GB VRAM) - text-line spotting"),
            ("infinity-parser2-flash", "INF Tech Infinity-Parser2 Flash 2B  (~2 GB VRAM) - full layout analysis")
        ];

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");

            for (int i = 0; i < SupportedModels.Length; i++)
            {
                string recommended = i == 0 ? " (recommended)" : "";
                Console.WriteLine($"{i} - {SupportedModels[i].Label}{recommended}");
            }

            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            LM model = LoadModel(input);

            // Each engine exposes its native level of spatial understanding.
            // Layout-analysis models (Infinity-Parser2) locate and classify
            // block-level regions (title, text, table, formula, figure, ...);
            // spotting models (PaddleOCR-VL) locate individual text lines.
            // Pick the richest intent the model supports.
            IReadOnlyList<VlmOcrIntent> supportedIntents = VlmOcr.GetSupportedIntents(model);
            VlmOcrIntent intent = supportedIntents.Contains(VlmOcrIntent.LayoutAnalysis)
                ? VlmOcrIntent.LayoutAnalysis
                : VlmOcrIntent.OcrWithCoordinates;

            Console.Clear();
            PrintBanner(intent);

            while (true)
            {
                Attachment? attachment = null;

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" - enter image or document path (or 'q' to quit):\n> ");

                    string path = Console.ReadLine() ?? string.Empty;
                    path = path.Trim();

                    if (string.Equals(path, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nDemo ended. Press any key to exit.");
                        Console.ReadKey();
                        return;
                    }

                    try
                    {
                        attachment = new Attachment(path);
                        Console.WriteLine();
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nError: Unable to open '{path}'.");
                        Console.WriteLine($"Details: {e.Message}");
                        Console.ResetColor();
                        Console.WriteLine("\nPlease check the file path and permissions, then try again.\n");
                    }
                }

                var ocr = new VlmOcr(model, intent)
                {
                    MaximumCompletionTokens = 8192
                };

                for (int pageIndex = 0; pageIndex < attachment.PageCount; pageIndex++)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n---------- Page {pageIndex + 1}/{attachment.PageCount} ----------");
                    Console.ResetColor();

                    Stopwatch sw = Stopwatch.StartNew();
                    VlmOcr.VlmOcrResult result = ocr.Run(attachment, pageIndex);
                    sw.Stop();

                    PageElement page = result.PageElement;

                    // Only elements with a real bounding box are positioned regions;
                    // an engine that could not produce coordinates yields plain text.
                    List<TextElement> regions = page.TextElements
                        .Where(e => e.Width > 0 && e.Height > 0)
                        .ToList();

                    int index = 0;

                    foreach (TextElement element in regions)
                    {
                        Console.Write($"  [{index,3}] ");

                        Console.ForegroundColor = GetConsoleColor(element.Category);
                        Console.Write($"{FormatCategory(element.Category),-16}");
                        Console.ResetColor();

                        Console.WriteLine(string.IsNullOrEmpty(element.Text)
                            ? "(no text content)"
                            : $"\"{Preview(element.Text)}\"");
                        Console.WriteLine($"        Position: ({element.Left:F1}, {element.Top:F1})  " +
                                          $"Size: {element.Width:F1} x {element.Height:F1}");
                        index++;
                    }

                    if (index == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("  No regions with coordinates detected.");
                        Console.ResetColor();
                        Console.WriteLine($"\n  Raw text:\n{page.Text}");
                    }
                    else
                    {
                        Console.WriteLine($"\n  Total regions: {index}");
                        PrintLegend(regions);
                    }

                    if (index > 0)
                    {
                        SaveAnnotatedImage(attachment, page, regions, pageIndex);
                    }

                    double elapsedSeconds = sw.Elapsed.TotalSeconds;

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("\n---------- Stats ----------");
                    Console.WriteLine($" elapsed time : {elapsedSeconds:F2} s");
                    Console.WriteLine($" gen. tokens  : {result.TextGeneration.GeneratedTokens.Count}");
                    Console.WriteLine($" stop reason  : {result.TextGeneration.TerminationReason}");
                    Console.WriteLine($" quality      : {Math.Round(result.TextGeneration.QualityScore, 2)}");
                    Console.WriteLine($" speed        : {Math.Round(result.TextGeneration.TokenGenerationRate, 2)} tok/s");
                    Console.WriteLine($" ctx usage    : {result.TextGeneration.ContextTokens.Count}/{result.TextGeneration.ContextSize}");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("----------------------------");
                    Console.ResetColor();
                }

                Console.Write("\nPress Enter to process another file, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    break;
                }

                Console.Clear();
                PrintBanner(intent);
            }
        }

        private static void PrintBanner(VlmOcrIntent intent)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit VLM OCR with Coordinates Demo");
            Console.ResetColor();

            if (intent == VlmOcrIntent.LayoutAnalysis)
            {
                Console.WriteLine("Mode: layout analysis. Every region is located and classified");
                Console.WriteLine("(title, text, table, formula, figure, captions, footers, ...),");
                Console.WriteLine("then drawn on the image with one color per category.\n");
            }
            else
            {
                Console.WriteLine("Mode: text spotting. Individual text lines are located with");
                Console.WriteLine("bounding boxes and drawn on the image.\n");
            }
        }

        /// <summary>
        /// Renders the page's regions onto the source image, one border color per
        /// layout category, and saves the result next to the input file.
        /// </summary>
        private static void SaveAnnotatedImage(
            Attachment attachment,
            PageElement page,
            List<TextElement> regions,
            int pageIndex)
        {
            string inputPath = attachment.Path;
            string annotatedPath = BuildAnnotatedPath(inputPath, pageIndex, attachment.PageCount);

            try
            {
                ImageBuffer image;

                string ext = Path.GetExtension(inputPath);

                if (IsImageExtension(ext) && attachment.PageCount == 1)
                {
                    // Single image file: load directly.
                    image = ImageBuffer.LoadAsRGB(inputPath);
                }
                else
                {
                    // Document (PDF, etc.): render the specific page.
                    image = PdfToImage.RenderPage(attachment, pageIndex);
                }

                try
                {
                    var canvas = new Canvas(image) { Antialiasing = true };

                    // Scale page coordinates to the rendered image dimensions.
                    double scaleX = page.Width > 0 ? image.Width / page.Width : 1;
                    double scaleY = page.Height > 0 ? image.Height / page.Height : 1;

                    foreach (TextElement element in regions)
                    {
                        var rect = Rectangle.FromSize(
                            element.Left * scaleX,
                            element.Top * scaleY,
                            element.Width * scaleX,
                            element.Height * scaleY);

                        var pen = new Pen(GetColor(element.Category), 2) { LineJoin = LineJoin.Miter };
                        canvas.DrawRectangle(rect, pen);
                    }

                    image.SaveAsPng(annotatedPath);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n  Annotated image saved to: {annotatedPath}");
                    Console.ResetColor();

                    try
                    {
                        Process.Start(new ProcessStartInfo(annotatedPath) { UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"  Could not auto-open file: {ex.Message}");
                        Console.ResetColor();
                    }
                }
                finally
                {
                    image.Dispose();
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n  Could not save annotated image: {e.Message}");
                Console.ResetColor();
            }
        }

        /// <summary>
        /// Prints the categories present on the page with their box colors,
        /// so the annotated image can be read at a glance.
        /// </summary>
        private static void PrintLegend(List<TextElement> regions)
        {
            var categories = regions
                .Select(e => e.Category)
                .Distinct()
                .ToList();

            if (categories.Count == 1 && categories[0] == LayoutElementCategory.Unknown)
            {
                // Spotting output carries no classification: a legend adds nothing.
                return;
            }

            Console.Write("\n  Legend: ");

            for (int i = 0; i < categories.Count; i++)
            {
                if (i > 0)
                {
                    Console.Write(", ");
                }

                Console.ForegroundColor = GetConsoleColor(categories[i]);
                Console.Write(FormatCategory(categories[i]));
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        /// <summary>
        /// The border color drawn on the annotated image for a layout category.
        /// Captions reuse a lighter shade of their parent element's color.
        /// </summary>
        private static Color32 GetColor(LayoutElementCategory category)
        {
            switch (category)
            {
                case LayoutElementCategory.Title: return new Color32(147, 51, 234);   // purple
                case LayoutElementCategory.Text: return new Color32(37, 99, 235);     // blue
                case LayoutElementCategory.Table: return new Color32(22, 163, 74);    // green
                case LayoutElementCategory.TableCaption: return new Color32(74, 222, 128);
                case LayoutElementCategory.TableFootnote: return new Color32(21, 128, 61);
                case LayoutElementCategory.Formula: return new Color32(13, 148, 136); // teal
                case LayoutElementCategory.FormulaCaption: return new Color32(45, 212, 191);
                case LayoutElementCategory.Figure: return new Color32(249, 115, 22);  // orange
                case LayoutElementCategory.FigureCaption: return new Color32(251, 146, 60);
                case LayoutElementCategory.FigureFootnote: return new Color32(194, 65, 12);
                case LayoutElementCategory.Header: return new Color32(120, 120, 120); // gray
                case LayoutElementCategory.Footer: return new Color32(120, 120, 120);
                case LayoutElementCategory.PageFootnote: return new Color32(87, 83, 78);
                default: return new Color32(255, 0, 0);                               // red
            }
        }

        /// <summary>
        /// The console color used for a category tag; the closest match to the
        /// color drawn on the annotated image.
        /// </summary>
        private static ConsoleColor GetConsoleColor(LayoutElementCategory category)
        {
            switch (category)
            {
                case LayoutElementCategory.Title: return ConsoleColor.Magenta;
                case LayoutElementCategory.Text: return ConsoleColor.Blue;
                case LayoutElementCategory.Table: return ConsoleColor.Green;
                case LayoutElementCategory.TableCaption: return ConsoleColor.DarkGreen;
                case LayoutElementCategory.TableFootnote: return ConsoleColor.DarkGreen;
                case LayoutElementCategory.Formula: return ConsoleColor.Cyan;
                case LayoutElementCategory.FormulaCaption: return ConsoleColor.DarkCyan;
                case LayoutElementCategory.Figure: return ConsoleColor.DarkYellow;
                case LayoutElementCategory.FigureCaption: return ConsoleColor.DarkYellow;
                case LayoutElementCategory.FigureFootnote: return ConsoleColor.DarkYellow;
                case LayoutElementCategory.Header: return ConsoleColor.DarkGray;
                case LayoutElementCategory.Footer: return ConsoleColor.DarkGray;
                case LayoutElementCategory.PageFootnote: return ConsoleColor.DarkGray;
                default: return ConsoleColor.Red;
            }
        }

        private static string FormatCategory(LayoutElementCategory category)
        {
            switch (category)
            {
                case LayoutElementCategory.Unknown: return "text line";
                case LayoutElementCategory.FigureCaption: return "figure caption";
                case LayoutElementCategory.FigureFootnote: return "figure footnote";
                case LayoutElementCategory.TableCaption: return "table caption";
                case LayoutElementCategory.TableFootnote: return "table footnote";
                case LayoutElementCategory.FormulaCaption: return "formula caption";
                case LayoutElementCategory.PageFootnote: return "page footnote";
                default: return category.ToString().ToLowerInvariant();
            }
        }

        /// <summary>
        /// Collapses a region's content (which can be multi-line Markdown, HTML,
        /// or LaTeX) into a single truncated console line.
        /// </summary>
        private static string Preview(string text, int maxLength = 70)
        {
            var sb = new StringBuilder(Math.Min(text.Length, maxLength + 3));
            bool lastWasSpace = false;

            foreach (char c in text)
            {
                char mapped = char.IsWhiteSpace(c) ? ' ' : c;

                if (mapped == ' ' && lastWasSpace)
                {
                    continue;
                }

                sb.Append(mapped);
                lastWasSpace = mapped == ' ';

                if (sb.Length >= maxLength)
                {
                    sb.Append("...");
                    break;
                }
            }

            return sb.ToString();
        }

        private static bool IsImageExtension(string extension)
        {
            return extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".tif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the output path for the annotated image.
        /// For multi-page documents, appends the page number.
        /// </summary>
        private static string BuildAnnotatedPath(string inputPath, int pageIndex, int pageCount)
        {
            string dir = Path.GetDirectoryName(inputPath) ?? ".";
            string name = Path.GetFileNameWithoutExtension(inputPath);

            if (pageCount > 1)
            {
                return Path.Combine(dir, $"{name}_page{pageIndex + 1}_annotated.png");
            }

            return Path.Combine(dir, $"{name}_annotated.png");
        }

        private static LM LoadModel(string input)
        {
            if (int.TryParse(input, out int index) &&
                index >= 0 &&
                index < SupportedModels.Length)
            {
                return LM.LoadFromModelID(
                    SupportedModels[index].ModelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            // Treat as custom model URI.
            return new LM(
                new Uri(input.Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                double percent = (double)bytesRead / contentLength.Value * 100;
                Console.Write($"\rDownloading: {percent:F1}%   ");
            }
            else
            {
                Console.Write($"\rDownloading: {bytesRead / 1024.0 / 1024.0:F1} MB   ");
            }

            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.WriteLine();
                _isDownloading = false;
            }

            Console.Write($"\rLoading: {progress * 100:F0}%   ");
            return true;
        }
    }
}
