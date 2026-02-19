using LMKit.Data;
using LMKit.Model;
using LMKit.Translation;
using System.Diagnostics;
using System.Text;

namespace language_detection_from_document
{
    internal class Program
    {
        static bool _isDownloading;

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
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

        private static bool OnLoadProgress(float progress)
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
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string? modelId = input?.Trim() switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            // Load model
            LM model;

            if (modelId != null)
            {
                model = LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                model = new LM(
                    new Uri(input.Trim('"')),
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }
            else
            {
                model = LM.LoadFromModelID(
                    "gemma3:4b",
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            Console.Clear();
            TextTranslation translator = new(model);

            while (true)
            {
                Console.Write("Enter the path to an image or PDF:\n\n> ");
                string? path = Console.ReadLine();

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
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message} Please check the file path and permissions.");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }
    }
}
