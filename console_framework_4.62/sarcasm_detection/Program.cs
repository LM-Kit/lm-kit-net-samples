using LMKit.Model;
using LMKit.TextAnalysis;
using System;
using System.Diagnostics;
using System.Text;

namespace sarcasm_detection
{
    internal class Program
    {
        /*
             Note: This model has been fine-tuned specifically for the English language. 
             For processing other languages, additional model fine-tuning may be required.
         */
        static readonly string DEFAULT_MODEL_PATH = @"https://huggingface.co/lm-kit/LM-Kit.Sarcasm_Detection-TinyLlama-1.1B-1T-OpenOrca-en-q4/resolve/main/LM-Kit.Sarcasm_Detection-TinyLlama-1.1B-1T-OpenOrca-en-q4.gguf?download=true";
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
            Uri modelUri = new Uri(DEFAULT_MODEL_PATH);
            LM model = new LM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"Please enter the text you would like to classify as either sarcastic or sincere.");
            Console.ResetColor();

            SarcasmDetection classifier = new SarcasmDetection(model);


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
                var isSarcastic = classifier.IsSarcastic(text);
                sw.Stop();

                Console.WriteLine($"Is sarcastic: {isSarcastic} - Elapsed: {Math.Round(sw.Elapsed.TotalSeconds, 2)} seconds - Confidence: {Math.Round(classifier.Confidence * 100, 1)} %");
            }

            Console.WriteLine("The program ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }
    }
}