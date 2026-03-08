using LMKit.Data;
using LMKit.Document.Layout;
using LMKit.Document.Search;
using LMKit.Extraction.Ocr;
using LMKit.Integrations.Tesseract;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace search_and_highlight
{
    internal class Program
    {
        private static bool _isDownloading;

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            PrintBanner();

            while (true)
            {
                // ── Step 1: Get input file path ──
                Attachment? attachment = null;
                string inputPath = string.Empty;

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" - enter PDF or image file path (or 'q' to quit):\n> ");

                    inputPath = Console.ReadLine() ?? string.Empty;
                    inputPath = inputPath.Trim().Trim('"');

                    if (string.Equals(inputPath, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nDemo ended. Press any key to exit.");
                        Console.ReadKey();
                        return;
                    }

                    try
                    {
                        attachment = new Attachment(inputPath);
                        Console.WriteLine();
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nError: Unable to open '{inputPath}'.");
                        Console.WriteLine($"Details: {e.Message}");
                        Console.ResetColor();
                        Console.WriteLine("\nPlease check the file path and permissions, then try again.\n");
                    }
                }

                // ── Step 2: Run OCR if no extractable text ──
                PageElement[]? pageElements = null;

                if (!attachment.HasText)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("No extractable text found. OCR is required to extract text for searching.");
                    Console.ResetColor();

                    try
                    {
                        pageElements = RunOcr(attachment, SelectOcrEngine());
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"OCR failed: {e.Message}\n");
                        Console.ResetColor();
                        attachment.Dispose();
                        continue;
                    }
                }

                // ── Step 3: Get search query ──
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant");
                Console.ResetColor();
                Console.Write(" - enter search query:\n> ");

                string query = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(query))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Empty query. Skipping.\n");
                    Console.ResetColor();
                    attachment.Dispose();
                    continue;
                }

                // ── Step 4: Select search mode ──
                SearchMode searchMode = SelectSearchMode();

                // ── Step 5: Run search and highlight ──
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\nSearching and highlighting...");
                Console.ResetColor();

                var options = new SearchHighlightOptions
                {
                    SearchMode = searchMode
                };

                Stopwatch sw = Stopwatch.StartNew();
                SearchHighlightResult result;

                try
                {
                    if (pageElements != null)
                    {
                        result = SearchHighlightEngine.Highlight(
                            attachment, query, options, pageElements);
                    }
                    else
                    {
                        result = SearchHighlightEngine.Highlight(
                            inputPath, query, options);
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nSearch failed: {e.Message}\n");
                    Console.ResetColor();
                    attachment.Dispose();
                    continue;
                }

                sw.Stop();

                // ── Step 7: Display results ──
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n---------- Results ----------");
                Console.ResetColor();
                Console.WriteLine($"  Query        : \"{result.Query}\"");
                Console.WriteLine($"  Search mode  : {result.SearchMode}");
                Console.WriteLine($"  Pages scanned: {result.ScannedPages}/{result.PageCount}");
                Console.WriteLine($"  Matches found: {result.TotalMatches}");

                if (result.LimitedByMaxResults)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("  (Results limited by MaxResults setting)");
                    Console.ResetColor();
                }

                int displayCount = Math.Min(result.Matches.Count, 10);

                for (int i = 0; i < displayCount; i++)
                {
                    var match = result.Matches[i];
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  [{i + 1}] Page {match.PageIndex + 1}: ");
                    Console.ResetColor();
                    Console.WriteLine($"\"{match.Text}\"");

                    if (!string.IsNullOrEmpty(match.Snippet))
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"       ...{match.Snippet}...");
                        Console.ResetColor();
                    }
                }

                if (result.Matches.Count > 10)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  ... and {result.Matches.Count - 10} more matches.");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n---------- Stats ----------");
                Console.WriteLine($"  Elapsed time : {sw.Elapsed.TotalSeconds:F2} s");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("----------------------------");
                Console.ResetColor();

                // ── Step 8: Save and auto-open ──
                if (result.TotalMatches > 0)
                {
                    string outputExtension = result.OutputMimeType == "application/pdf" ? ".pdf" : ".png";
                    string dir = Path.GetDirectoryName(inputPath) ?? ".";
                    string name = Path.GetFileNameWithoutExtension(inputPath);
                    string outputPath = Path.Combine(dir, $"{name}_highlighted{outputExtension}");

                    File.WriteAllBytes(outputPath, result.OutputData);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\nHighlighted file saved to: {outputPath}");
                    Console.ResetColor();

                    try
                    {
                        Process.Start(new ProcessStartInfo(outputPath) { UseShellExecute = true });
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Could not auto-open file: {e.Message}");
                        Console.ResetColor();
                    }
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("\nNo matches found. No output file generated.");
                    Console.ResetColor();
                }

                attachment.Dispose();

                // ── Loop or quit ──
                Console.Write("\nPress Enter to process another file, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    break;
                }

                Console.Clear();
                PrintBanner();
            }
        }

        private static void PrintBanner()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Search and Highlight Demo");
            Console.ResetColor();
            Console.WriteLine("Search text in PDFs and images, and produce highlighted output.\n");
        }

        private static string SelectOcrEngine()
        {
            Console.WriteLine("\nSelect OCR engine:\n");
            Console.WriteLine("  0 - Tesseract OCR (data files downloaded automatically)");
            Console.WriteLine("  1 - PaddleOCR-VL  (VLM-based, model downloaded automatically)");
            Console.Write("\n> ");

            string choice = Console.ReadLine()?.Trim() ?? "0";
            Console.WriteLine();
            return choice;
        }

        private static SearchMode SelectSearchMode()
        {
            Console.WriteLine("\nSelect search mode:\n");
            Console.WriteLine("  0 - Text  (exact substring match, default)");
            Console.WriteLine("  1 - Regex (regular expression pattern)");
            Console.WriteLine("  2 - Fuzzy (approximate matching)");
            Console.Write("\n> ");

            string modeInput = Console.ReadLine()?.Trim() ?? "0";

            SearchMode mode = modeInput switch
            {
                "1" => SearchMode.Regex,
                "2" => SearchMode.Fuzzy,
                _ => SearchMode.Text
            };

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"Using search mode: {mode}");
            Console.ResetColor();

            return mode;
        }

        private static PageElement[] RunOcr(Attachment attachment, string ocrChoice)
        {
            int pageCount = attachment.PageCount;
            var pageElements = new PageElement[pageCount];

            if (ocrChoice == "1")
            {
                LM model = LM.LoadFromModelID(
                    "paddleocr-vl:0.9b",
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);

                if (_isDownloading)
                {
                    Console.WriteLine();
                    _isDownloading = false;
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\nRunning PaddleOCR-VL...");
                Console.ResetColor();

                var ocr = new VlmOcr(model, VlmOcrIntent.OcrWithCoordinates);

                for (int i = 0; i < pageCount; i++)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  OCR page {i + 1}/{pageCount}...");
                    Console.ResetColor();

                    var result = ocr.Run(attachment, i);
                    pageElements[i] = result.PageElement;
                }

                model.Dispose();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("Running Tesseract OCR...");
                Console.ResetColor();

                using var ocr = new TesseractOcr();

                for (int i = 0; i < pageCount; i++)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  OCR page {i + 1}/{pageCount}...");
                    Console.ResetColor();

                    OcrResult result = ocr.RunAsync(attachment, i).GetAwaiter().GetResult();
                    pageElements[i] = result.PageElement;
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  OCR complete. {pageCount} page(s) processed.\n");
            Console.ResetColor();

            return pageElements;
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
