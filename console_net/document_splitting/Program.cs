using LMKit.Data;
using LMKit.Document.Pdf;
using LMKit.Extraction;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace document_splitting
{
    internal class Program
    {
        private static bool _isDownloading;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen 3 8B (requires approximately 6.5 GB of VRAM) (recommended)");
            Console.WriteLine("1 - Alibaba Qwen 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B (requires approximately 5.7 GB of VRAM)");
            Console.WriteLine("3 - Google Gemma 3 12B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - MiniCPM o 4.5 9B (requires approximately 5.9 GB of VRAM)");
            Console.WriteLine("5 - Mistral Ministral 3 8B (requires approximately 6.5 GB of VRAM)");
            Console.WriteLine("6 - Mistral Ministral 3 14B (requires approximately 12 GB of VRAM)");

            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine() ?? string.Empty;
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:8b").ModelUri.ToString();
                    break;
                case "1":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:4b").ModelUri.ToString();
                    break;
                case "2":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:4b").ModelUri.ToString();
                    break;
                case "3":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:12b").ModelUri.ToString();
                    break;
                case "4":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("minicpm-o-45").ModelUri.ToString();
                    break;
                case "5":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("ministral3:8b").ModelUri.ToString();
                    break;
                case "6":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("ministral3:14b").ModelUri.ToString();
                    break;
                default:
                    modelLink = input.Trim().Trim('"').Trim('\u201C');
                    break;
            }

            // Loading model
            Uri modelUri = new(modelLink);
            LM model = new(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();

            var splitter = new DocumentSplitting(model);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Document Splitting Demo");
            Console.ResetColor();
            Console.WriteLine("Automatically detect and split multi-document PDFs using AI vision.\n");

            while (true)
            {
                Attachment? attachment = null;
                string? pdfPath = null;

                // Ask for PDF path
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" \u2014 enter PDF file path (or 'q' to quit):\n> ");

                    string path = Console.ReadLine() ?? string.Empty;
                    path = path.Trim();

                    if (string.Equals(path, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nExiting. Bye \U0001F44B");
                        return;
                    }

                    try
                    {
                        attachment = new Attachment(path);
                        pdfPath = path;
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

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"Analyzing {attachment.PageCount} page(s)...");
                Console.ResetColor();

                // Run document splitting
                Stopwatch sw = Stopwatch.StartNew();
                DocumentSplittingResult result = splitter.Split(attachment);
                sw.Stop();

                // Display results
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500 Results \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                Console.ResetColor();

                Console.WriteLine($"  Documents found     : {result.DocumentCount}");
                Console.WriteLine($"  Multiple documents  : {result.ContainsMultipleDocuments}");
                Console.WriteLine($"  Confidence          : {result.Confidence:P0}");
                Console.WriteLine();

                foreach (DocumentSegment segment in result.Segments)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"  \u25B6 ");
                    Console.ResetColor();

                    string pageRange = segment.StartPage == segment.EndPage
                        ? $"Page {segment.StartPage}"
                        : $"Pages {segment.StartPage}-{segment.EndPage}";

                    Console.Write(pageRange);
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($" ({segment.PageCount} page{(segment.PageCount > 1 ? "s" : "")})");
                    Console.ResetColor();

                    if (!string.IsNullOrEmpty(segment.Label))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write($"  {segment.Label}");
                        Console.ResetColor();
                    }

                    Console.WriteLine();
                }

                // Stats
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500 Stats \u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                Console.WriteLine($"  elapsed time : {sw.Elapsed.TotalSeconds:F2} s");
                Console.WriteLine($"  total pages  : {attachment.PageCount}");
                Console.WriteLine($"  segments     : {result.DocumentCount}");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");
                Console.ResetColor();

                // Offer to split the document if multiple segments were found
                if (result.ContainsMultipleDocuments)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write($" \u2014 {result.DocumentCount} documents were detected. Would you like to split them into separate PDF files? (y/n)\n> ");

                    string splitAnswer = Console.ReadLine()?.Trim() ?? string.Empty;

                    if (string.Equals(splitAnswer, "y", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(splitAnswer, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        // Ask for output directory
                        string defaultDir = Path.Combine(
                            Path.GetDirectoryName(Path.GetFullPath(pdfPath!)) ?? ".",
                            Path.GetFileNameWithoutExtension(pdfPath!) + "_split");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("\nAssistant");
                        Console.ResetColor();
                        Console.Write($" \u2014 enter output directory (press Enter for '{defaultDir}'):\n> ");

                        string outputDir = Console.ReadLine()?.Trim() ?? string.Empty;

                        if (string.IsNullOrEmpty(outputDir))
                        {
                            outputDir = defaultDir;
                        }

                        string prefix = Path.GetFileNameWithoutExtension(pdfPath!);

                        try
                        {
                            Console.ForegroundColor = ConsoleColor.Blue;
                            Console.WriteLine($"\nSplitting into '{outputDir}'...");
                            Console.ResetColor();

                            List<string> outputFiles = PdfSplitter.SplitToFiles(
                                attachment,
                                result,
                                outputDir,
                                prefix);

                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"\n\u2714 Successfully created {outputFiles.Count} file(s):");
                            Console.ResetColor();

                            for (int i = 0; i < outputFiles.Count; i++)
                            {
                                DocumentSegment segment = result.Segments.ElementAt(i);
                                string label = !string.IsNullOrEmpty(segment.Label) ? $"  ({segment.Label})" : "";

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write($"  \u25B6 ");
                                Console.ResetColor();
                                Console.Write(Path.GetFileName(outputFiles[i]));
                                Console.ForegroundColor = ConsoleColor.DarkGray;
                                Console.Write($"  {segment.PageCount} page{(segment.PageCount > 1 ? "s" : "")}");
                                Console.ForegroundColor = ConsoleColor.Cyan;
                                Console.WriteLine(label);
                                Console.ResetColor();
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"\nError splitting document: {ex.Message}");
                            Console.ResetColor();
                        }
                    }
                }

                Console.Write("\nPress Enter to process another PDF, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nExiting. Bye \U0001F44B");
                    break;
                }

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("LM-Kit Document Splitting Demo");
                Console.ResetColor();
                Console.WriteLine("Type the path to a PDF (or 'q' to quit).\n");
            }
        }
    }
}
