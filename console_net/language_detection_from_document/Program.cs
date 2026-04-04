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
            Console.WriteLine("Select a vision-language model to use for language detection:\n");
            Console.WriteLine("0 - Z.ai GLM-V 4.6 Flash 10B  (~7 GB VRAM)");
            Console.WriteLine("1 - MiniCPM o 4.5 9B          (~5.9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3.5 2B       (~2 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3.5 4B       (~3.5 GB VRAM)");
            Console.WriteLine("4 - Alibaba Qwen 3.5 9B       (~7 GB VRAM) [Recommended]");
            Console.WriteLine("6 - Google Gemma 4 E4B         (~6 GB VRAM)");
            Console.WriteLine("7 - Alibaba Qwen 3.5 27B      (~18 GB VRAM)");
            Console.WriteLine("8 - Mistral Ministral 3 8B     (~6.5 GB VRAM)");
            Console.Write("\nOther: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            string? modelId = input?.Trim() switch
            {
                "0" => "glm-4.6v-flash",
                "1" => "minicpm-o-45",
                "2" => "qwen3.5:2b",
                "3" => "qwen3.5:4b",
                "4" => "qwen3.5:9b",
                "6" => "gemma4:e4b",
                "7" => "qwen3.5:27b",
                "8" => "ministral3:8b",
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
                string uri = input.Trim('"');
                if (!uri.Contains("://"))
                    model = LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
                else
                    model = new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            else
            {
                model = LM.LoadFromModelID(
                    "qwen3.5:9b",
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
