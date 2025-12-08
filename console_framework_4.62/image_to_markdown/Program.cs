using System;
using System.Diagnostics;
using System.Text;
using LMKit.Data;
using LMKit.Extraction.Ocr;
using LMKit.Model;

namespace multi_turn_chat_with_vision
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
            Console.WriteLine("0 - LightOn LightOnOCR 1025 1B (requires approximately 2 GB of VRAM)");
            Console.WriteLine("1 - MiniCPM 2.6 o 8.1B (requires approximately 5.9 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 2B (requires approximately 2.5 GB of VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen 3 8B (requires approximately 6.5 GB of VRAM)");
            Console.WriteLine("5 - Google Gemma 3 4B (requires approximately 5.7 GB of VRAM)");
            Console.WriteLine("6 - Google Gemma 3 12B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("7 - Mistral Ministral 3 3B (requires approximately 3.5 GB of VRAM)");
            Console.WriteLine("8 - Mistral Ministral 3 8B (requires approximately 6.5 GB of VRAM)");
            Console.WriteLine("9 - Mistral Ministral 3 14B (requires approximately 12 GB of VRAM)");

            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine() ?? string.Empty;
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("lightonocr1025:1b").ModelUri.ToString();
                    break;
                case "1":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("minicpm-o").ModelUri.ToString();
                    break;
                case "2":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:2b").ModelUri.ToString();
                    break;
                case "3":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:4b").ModelUri.ToString();
                    break;
                case "4":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:8b").ModelUri.ToString();
                    break;
                case "5":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:4b").ModelUri.ToString();
                    break;
                case "6":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:12b").ModelUri.ToString();
                    break;
                case "7":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("ministral3:3b").ModelUri.ToString();
                    break;
                case "8":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("ministral3:8b").ModelUri.ToString();
                    break;
                case "9":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("ministral3:14b").ModelUri.ToString();
                    break;
                default:
                    modelLink = input.Trim().Trim('"').Trim('"');
                    break;
            }

            // Loading model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();

            VlmOcr ocr = new VlmOcr(model);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Vision OCR Demo");
            Console.ResetColor();

            while (true)
            {
                Attachment attachment = null;

                // Ask for image path (with 'q' to quit and nice error messages)
                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" — enter image path (or 'q' to quit):\n> ");

                    string path = Console.ReadLine() ?? string.Empty;
                    path = path.Trim();

                    if (string.Equals(path, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nExiting. Bye 👋");
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

                // Run OCR
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("\n────────── Result ──────────");
                Console.ResetColor();

                Stopwatch sw = Stopwatch.StartNew();
                var result = ocr.Run(attachment);
                sw.Stop();

                Console.WriteLine(result.PageElement.Text);

                double elapsedSeconds = sw.Elapsed.TotalSeconds;

                // Stats section (includes elapsed time in seconds)
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("\n────────── Stats ──────────");
                Console.WriteLine($" elapsed time : {elapsedSeconds:F2} s");
                Console.WriteLine($" gen. tokens  : {result.TextGeneration.GeneratedTokens.Count}");
                Console.WriteLine($" stop reason  : {result.TextGeneration.TerminationReason}");
                Console.WriteLine($" quality      : {Math.Round(result.TextGeneration.QualityScore, 2)}");
                Console.WriteLine($" speed        : {Math.Round(result.TextGeneration.TokenGenerationRate, 2)} tok/s");
                Console.WriteLine($" ctx usage    : {result.TextGeneration.ContextTokens.Count}/{result.TextGeneration.ContextSize}");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("────────────────────────────");
                Console.ResetColor();

                Console.Write("Press Enter to process another image, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nExiting. Bye 👋");
                    break;
                }

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("LM-Kit Vision OCR Demo");
                Console.ResetColor();
                Console.WriteLine("Type the path to an image (or 'q' to quit).\n");
            }
        }
    }
}