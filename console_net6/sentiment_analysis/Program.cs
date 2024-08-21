using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;


namespace sentiment_analysis
{
    internal class Program
    {
        /*
            # Note: This model has been fine-tuned specifically for the English language. 
            # For processing multilingual input, please use another model such as LLama3.
         */
        static readonly string DEFAULT_MODEL_PATH = @"https://huggingface.co/lm-kit/LM-Kit.Sentiment_Analysis-TinyLlama-1.1B-1T-OpenOrca-en-q4/resolve/main/LM-Kit.Sentiment_Analysis-TinyLlama-1.1B-1T-OpenOrca-en-q4.gguf?download=true";
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
            LMKit.Licensing.LicenseManager.SetLicenseKey(""); //set an optional license key here if available.
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            //Loading model
            Uri modelUri = new Uri(DEFAULT_MODEL_PATH);
            LLM model = new LLM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Please enter text to classify as positive or negative sentiment.");
            Console.ResetColor();

            SentimentAnalysis classifier = new SentimentAnalysis(model)
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