using LMKit.Data;
using LMKit.Model;
using LMKit.Translation;
using System;
using System.Diagnostics;

namespace language_detection_from_image
{
    internal class Program
    {

        static bool _isDownloading;

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

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - MiniCPM 2.6 o Vision 8.1B (requires approximately 5.9 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3 Vision 2B (requires approximately 2.5 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 Vision 4B (requires approximately 4.5 GB of VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 Vision 8B (requires approximately 6.5 GB of VRAM)");
            Console.WriteLine("4 - Google Gemma 3 Vision 4B (requires approximately 5.7 GB of VRAM)");
            Console.WriteLine("5 - Google Gemma 3 Vision 12B (requires approximately 11 GB of VRAM)");

            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("minicpm-o").ModelUri.ToString();
                    break;
                case "1":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:2b").ModelUri.ToString();
                    break;
                case "2":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:4b").ModelUri.ToString();
                    break;
                case "3":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("qwen3-vl:8b").ModelUri.ToString();
                    break;
                case "4":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:4b").ModelUri.ToString();
                    break;
                case "5":
                    modelLink = ModelCard.GetPredefinedModelCardByModelID("gemma3:12b").ModelUri.ToString();
                    break;
                default:
                    modelLink = input.Trim().Trim('"').Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            TextTranslation translator = new TextTranslation(model);

            while (true)
            {
                Console.Write("Enter the path to an image attachment:\n\n> ");
                string path = Console.ReadLine();

                if (string.IsNullOrEmpty(path))
                {
                    break;
                }

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    var attachment = new Attachment(path);
                    var language = translator.DetectLanguage(attachment);
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"The detected language is {language} | processing time: {sw.Elapsed}\n\n");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message} Please check the file path and permissions.");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("The program ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }
    }
}