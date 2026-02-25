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

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - MiniCPM o 4.5 9B       (~5.9 GB VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3 VL 2B   (~2.5 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 VL 4B   (~4.5 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 VL 8B   (~6.5 GB VRAM)");
            Console.WriteLine("4 - Google Gemma 3 4B       (~5.7 GB VRAM)");
            Console.WriteLine("5 - Google Gemma 3 12B      (~11 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen 3.5 27B   (~18 GB VRAM)");
            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            LM model = LoadModel(input);

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

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" - enter PDF file path (or 'q' to quit):\n> ");

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

                Stopwatch sw = Stopwatch.StartNew();
                DocumentSplittingResult result = splitter.Split(attachment);
                sw.Stop();

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n---------- Results ----------");
                Console.ResetColor();

                Console.WriteLine($"  Documents found     : {result.DocumentCount}");
                Console.WriteLine($"  Multiple documents  : {result.ContainsMultipleDocuments}");
                Console.WriteLine($"  Confidence          : {result.Confidence:P0}");
                Console.WriteLine();

                foreach (DocumentSegment segment in result.Segments)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"  > ");
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

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine($"\n---------- Stats ------------");
                Console.WriteLine($"  elapsed time : {sw.Elapsed.TotalSeconds:F2} s");
                Console.WriteLine($"  total pages  : {attachment.PageCount}");
                Console.WriteLine($"  segments     : {result.DocumentCount}");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("-----------------------------");
                Console.ResetColor();

                if (result.ContainsMultipleDocuments)
                {
                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write($" - {result.DocumentCount} documents were detected. Would you like to split them into separate PDF files? (y/n)\n> ");

                    string splitAnswer = Console.ReadLine()?.Trim() ?? string.Empty;

                    if (string.Equals(splitAnswer, "y", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(splitAnswer, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        string defaultDir = Path.Combine(
                            Path.GetDirectoryName(Path.GetFullPath(pdfPath!)) ?? ".",
                            Path.GetFileNameWithoutExtension(pdfPath!) + "_split");

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write("\nAssistant");
                        Console.ResetColor();
                        Console.Write($" - enter output directory (press Enter for '{defaultDir}'):\n> ");

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
                            Console.WriteLine($"\nSuccessfully created {outputFiles.Count} file(s):");
                            Console.ResetColor();

                            for (int i = 0; i < outputFiles.Count; i++)
                            {
                                DocumentSegment segment = result.Segments.ElementAt(i);
                                string label = !string.IsNullOrEmpty(segment.Label) ? $"  ({segment.Label})" : "";

                                Console.ForegroundColor = ConsoleColor.Yellow;
                                Console.Write($"  > ");
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
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    break;
                }

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("LM-Kit Document Splitting Demo");
                Console.ResetColor();
                Console.WriteLine("Type the path to a PDF (or 'q' to quit).\n");
            }
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "minicpm-o-45",
                "1" => "qwen3-vl:2b",
                "2" => "qwen3-vl:4b",
                "3" => "qwen3-vl:8b",
                "4" => "gemma3:4b",
                "5" => "gemma3:12b",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

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
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading: {progress * 100:F0}%   ");
            return true;
        }
    }
}
