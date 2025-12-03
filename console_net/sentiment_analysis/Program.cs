using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;


namespace sentiment_analysis
{
    internal class Program
    {
        static readonly string DEFAULT_MODEL_PATH = @"https://huggingface.co/lm-kit/lm-kit-sentiment-analysis-2.0-1b-gguf/resolve/main/lm-kit-sentiment-analysis-2.0-1b-q4.gguf";
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
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            //Loading model
            Uri modelUri = new(DEFAULT_MODEL_PATH);
            LM model = new(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Please enter text to classify as positive or negative sentiment.");
            Console.ResetColor();

            SentimentAnalysis classifier = new(model)
            {
                NeutralSupport = false
            };


            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nContent: ");
                Console.ResetColor();

                string text = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\nCategory: ");
                Console.ResetColor();

                Stopwatch sw = Stopwatch.StartNew();
                var category = classifier.GetSentimentCategory(text);
                sw.Stop();

                Console.WriteLine($"Category: {category} - Elapsed: {Math.Round(sw.Elapsed.TotalSeconds, 2)} seconds - Confidence: {Math.Round(classifier.Confidence * 100, 1)} %");
            }

            Console.WriteLine("The program ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }
    }
}