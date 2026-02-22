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
            ("paddleocr-vl:0.9b", "PaddlePaddle PaddleOCR VL 1.5 0.9B  (~1 GB VRAM)")
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

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit VLM OCR with Coordinates Demo");
            Console.ResetColor();
            Console.WriteLine("Detects text regions with bounding boxes and draws them on the image.\n");

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

                var ocr = new VlmOcr(model, VlmOcrIntent.OcrWithCoordinates)
                {
                    MaximumCompletionTokens = 4096
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

                    // ── Print detected text regions ──
                    int index = 0;

                    foreach (TextElement element in page.TextElements)
                    {
                        Console.WriteLine($"  [{index}] \"{element.Text}\"");
                        Console.WriteLine($"       Position: ({element.Left:F1}, {element.Top:F1})  " +
                                          $"Size: {element.Width:F1} x {element.Height:F1}");
                        index++;
                    }

                    if (index == 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine("  No text regions with coordinates detected.");
                        Console.ResetColor();
                        Console.WriteLine($"\n  Raw text:\n{page.Text}");
                    }
                    else
                    {
                        Console.WriteLine($"\n  Total regions: {index}");
                    }

                    // ── Draw bounding boxes on the image ──
                    if (index > 0)
                    {
                        string inputPath = attachment.Path;

                        string annotatedPath = BuildAnnotatedPath(inputPath, pageIndex, attachment.PageCount);

                        try
                        {
                            ImageBuffer image;
                            bool ownImage;

                            if (attachment.PageCount > 1)
                            {
                                // Multi-page document (e.g. PDF): render the specific page.
                                image = PdfToImage.RenderPage(attachment, pageIndex);
                                ownImage = true;
                            }
                            else
                            {
                                // Single image file: load directly.
                                image = ImageBuffer.LoadAsRGB(inputPath);
                                ownImage = true;
                            }

                            try
                            {
                                var canvas = new Canvas(image) { Antialiasing = true };
                                var pen = new Pen(new Color32(255, 0, 0), 2) { LineJoin = LineJoin.Miter };

                                foreach (TextElement element in page.TextElements)
                                {
                                    var rect = Rectangle.FromSize(
                                        element.Left,
                                        element.Top,
                                        element.Width,
                                        element.Height);

                                    canvas.DrawRectangle(rect, pen);
                                }

                                image.SaveAsPng(annotatedPath);

                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine($"\n  Annotated image saved to: {annotatedPath}");
                                Console.ResetColor();
                            }
                            finally
                            {
                                if (ownImage)
                                {
                                    image.Dispose();
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\n  Could not save annotated image: {e.Message}");
                            Console.ResetColor();
                        }
                    }

                    // ── Stats ──
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
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("LM-Kit VLM OCR with Coordinates Demo");
                Console.ResetColor();
                Console.WriteLine("Detects text regions with bounding boxes and draws them on the image.\n");
            }
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
