using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace sarcasm_detection
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

            // Load model (fine-tuned specifically for English language)
            LM model = LM.LoadFromModelID(
                "lmkit-sarcasm-detection",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Please enter the text you would like to classify as either sarcastic or sincere.");
            Console.ResetColor();

            SarcasmDetection classifier = new(model);

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
                var isSarcastic = classifier.IsSarcastic(text);
                sw.Stop();

                Console.WriteLine($"Is sarcastic: {isSarcastic} - Elapsed: {Math.Round(sw.Elapsed.TotalSeconds, 2)} seconds - Confidence: {Math.Round(classifier.Confidence * 100, 1)} %");
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }
    }
}
