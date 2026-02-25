using LMKit.Extraction;
using LMKit.Integrations.Tesseract;
using LMKit.Model;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace invoice_data_extraction
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
            Console.WriteLine("Select a vision-language model to use for extraction:\n");
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

            var textExtraction = new TextExtraction(model);

            string schemaJson = File.ReadAllText("schema.json");
            textExtraction.SetElementsFromJsonSchema(schemaJson);

            var ocrEngine = new TesseractOcr
            {
                EnableLanguageDetection = true,
                EnableModelDownload = true,
                EnableOrientationDetection = true
            };

            ocrEngine.LanguageDetected += lang =>
                Console.WriteLine($"Detected language: {lang}");
            ocrEngine.OrientationDetected += angle =>
                Console.WriteLine($"Detected orientation: {angle} degrees");

            textExtraction.OcrEngine = ocrEngine;

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Select the invoice document to process:\n");
                Console.WriteLine("0 - invoice_fr.png");
                Console.WriteLine("1 - invoice_spa.png");
                Console.WriteLine("2 - invoice_eng.png");
                Console.WriteLine("3 - invoice_eng2.png");
                Console.WriteLine("Or enter a custom document file path");
                Console.Write("\n> ");
                input = Console.ReadLine() ?? string.Empty;

                string documentPath = GetDocumentPath(input.Trim('"'));

                Console.Clear();
                OpenDocument(documentPath);

                textExtraction.SetContent(new LMKit.Data.Attachment(documentPath));

                Console.WriteLine($"\nExtracting structured data from document {Path.GetFileName(documentPath)}...\n");
                var stopwatch = Stopwatch.StartNew();
                var extractionResult = textExtraction.Parse();
                stopwatch.Stop();

                WriteColor("\nExtraction results:\n", ConsoleColor.Green);
                foreach (var element in extractionResult.Elements)
                {
                    Console.Write($"{element.TextExtractionElement.Name}: ");
                    WriteColor(element.ToString(), ConsoleColor.Blue, addNL: false);
                    Console.WriteLine();
                }

                WriteColor("\nJSON Output:\n", ConsoleColor.Green);
                Console.WriteLine(extractionResult.Json);

                WriteColor($"\nCompleted in {stopwatch.Elapsed.TotalSeconds:0.00} seconds. Press any key to continue...",
                           ConsoleColor.Green);
                Console.ReadKey(intercept: true);
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

        private static string GetDocumentPath(string selection)
        {
            return selection switch
            {
                "0" => Path.Combine(AppContext.BaseDirectory, "examples", "invoice_fr.png"),
                "1" => Path.Combine(AppContext.BaseDirectory, "examples", "invoice_spa.png"),
                "2" => Path.Combine(AppContext.BaseDirectory, "examples", "invoice_eng.png"),
                "3" => Path.Combine(AppContext.BaseDirectory, "examples", "invoice_eng2.jpg"),
                _ => selection
            };
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

        private static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.Write(text);
            }
            Console.ResetColor();
        }

        private static void OpenDocument(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", filePath);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", filePath);
            }
        }
    }
}
