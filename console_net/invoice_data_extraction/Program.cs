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


        private static bool ModelDownloadingProgress(
            string path,
            long? contentLength,
            long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double percent = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model: {percent:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model: {bytesRead} bytes transferred");
            }

            return true;
        }


        private static bool ModelLoadingProgress(float progress)
        {
            // Clear download message once loading begins
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model: {Math.Round(progress * 100)}%");
            return true;
        }


        private static void Main(string[] args)
        {
            // Optionally set a license key; use a community license from https://lm-kit.com/products/community-edition/ if available.
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            // Enable UTF-8 support for proper display of multilingual text.
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Select a vision-language model to use for extraction:\n");
            Console.WriteLine("0 - MiniCPM o 4.5 9B (~5.9 GB VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3 2B (~2.5 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 4B (~4.5 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 8B (~6.5 GB VRAM)");
            Console.WriteLine("4 - Google Gemma 3 4B (~5.7 GB VRAM)");
            Console.WriteLine("5 - Google Gemma 3 12B (~11 GB VRAM)");
            Console.WriteLine("6 - Mistral Pixtral 12B (~12 GB VRAM)");
            Console.Write("Other entry: custom model URI\n\n> ");

            // Read user selection or custom URI
            string input = Console.ReadLine() ?? string.Empty;
            string modelLink = ResolveModelUri(input.Trim());

            // Instantiate and load the selected model with progress callbacks
            var modelUri = new Uri(modelLink);
            var model = new LM(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            // Initialize text extraction engine with loaded model
            var textExtraction = new TextExtraction(model);

            // Configure the extraction fields by loading definitions from a JSON schema file.
            // Alternatively, extraction fields can be defined manually via the `textExtraction.Elements` property.
            string schemaJson = File.ReadAllText("schema.json");
            textExtraction.SetElementsFromJsonSchema(schemaJson);

            // Attach an optional OCR engine to improve VLM accuracy.
            // You can also use other pre‑integrated or custom OCR engines. See the available list at:
            // https://docs.lm-kit.com/lm-kit-net/api/LMKit.Extraction.Ocr.OcrEngine.html
            var ocrEngine = new TesseractOcr
            {
                EnableLanguageDetection = true,
                EnableModelDownload = true,
                EnableOrientationDetection = true
            };

            // Log detected invoice language and orientation to console
            ocrEngine.LanguageDetected += lang =>
                Console.WriteLine($"Detected language: {lang}");
            ocrEngine.OrientationDetected += angle =>
                Console.WriteLine($"Detected orientation: {angle} degrees");

            textExtraction.OcrEngine = ocrEngine;

            // Continuous loop: allow user to process multiple invoices in one session
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
                // Display the selected invoice for visual confirmation
                OpenDocument(documentPath);

                // Set input content
                textExtraction.SetContent(new LMKit.Data.Attachment(documentPath));

                Console.WriteLine($"\nExtracting structured data from document {Path.GetFileName(documentPath)}...\n");
                var stopwatch = Stopwatch.StartNew();
                var extractionResult = textExtraction.Parse();
                stopwatch.Stop();

                // Output each extracted field and its value
                WriteColor("\nExtraction results:\n", ConsoleColor.Green);
                foreach (var element in extractionResult.Elements)
                {
                    Console.Write($"{element.TextExtractionElement.Name}: ");
                    WriteColor(element.ToString(), ConsoleColor.Blue, addNL: false);
                    Console.WriteLine();
                }

                // Present the full JSON payload for integration or storage
                WriteColor("\nJSON Output:\n", ConsoleColor.Green);
                Console.WriteLine(extractionResult.Json);

                WriteColor($"\nCompleted in {stopwatch.Elapsed.TotalSeconds:0.00} seconds. Press any key to continue...",
                           ConsoleColor.Green);
                Console.ReadKey(intercept: true);
            }
        }

        private static string ResolveModelUri(string input)
        {
            return input switch
            {
                "0" => ModelCard.GetPredefinedModelCardByModelID("minicpm-o-45").ModelUri.ToString(),
                "1" => ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:2b").ModelUri.ToString(),
                "2" => ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:4b").ModelUri.ToString(),
                "3" => ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:8b").ModelUri.ToString(),
                "4" => ModelCard.GetPredefinedModelCardByModelID("gemma3:4b").ModelUri.ToString(),
                "5" => ModelCard.GetPredefinedModelCardByModelID("gemma3:12b").ModelUri.ToString(),
                "6" => ModelCard.GetPredefinedModelCardByModelID("pixtral").ModelUri.ToString(),
                _ => input.Trim('"')
            };
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
