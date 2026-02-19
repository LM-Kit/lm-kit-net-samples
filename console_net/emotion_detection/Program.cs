using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace emotion_detection
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

        private static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4:14.7b",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(new Uri(input!.Trim().Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
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
            Console.Write("Other: Custom model URI\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "0");

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Please enter text to classify the emotion. The emotion will be identified as one of the following: neutral, happiness, anger, sadness, or fear.");
            Console.ResetColor();

            EmotionDetection classifier = new(model)
            {
                NeutralSupport = true
            };

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nContent: ");
                Console.ResetColor();

                string? text = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\nCategory: ");
                Console.ResetColor();

                Stopwatch sw = Stopwatch.StartNew();
                var category = classifier.GetEmotionCategory(text);
                sw.Stop();

                Console.WriteLine($"Category: {category} - Elapsed: {Math.Round(sw.Elapsed.TotalSeconds, 2)} seconds - Confidence: {Math.Round(classifier.Confidence * 100, 1)} %");
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }
    }
}
