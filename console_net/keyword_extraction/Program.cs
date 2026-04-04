using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace keyword_extraction
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

            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim().Trim('"') : "qwen3.5:9b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Select a vision-language model to use for keyword extraction:\n");
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
            LM model = LoadModel(input ?? "0");

            KeywordExtraction keywordExtraction = new(model)
            {
                KeywordCount = 8
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Please enter the path to an image, document or text file:\n");
                Console.Write("\n> ");
                string inputFilePath = Console.ReadLine()?.Trim().Trim('"') ?? "";

                if (!File.Exists(inputFilePath))
                {
                    WriteColor("invalid file path. Hit any key to retry.", ConsoleColor.Red);
                    _ = Console.ReadKey();
                    continue;
                }

                Console.Clear();

                var attachment = new LMKit.Data.Attachment(inputFilePath);

                Console.WriteLine($"\n\nTrying to extract {keywordExtraction.KeywordCount} keywords...\n");
                Stopwatch sw = Stopwatch.StartNew();
                var keywords = keywordExtraction.ExtractKeywords(attachment);
                sw.Stop();

                WriteColor("\nExtracted keywords:\n", ConsoleColor.Green);

                foreach (var item in keywords)
                {
                    Console.WriteLine($"{item.Value}");
                }

                int wordCount = attachment.GetText().Split([" ", "\r\n", "\n", "\t"], StringSplitOptions.RemoveEmptyEntries).Length;

                WriteColor("\nExtraction done in " + sw.Elapsed.TotalSeconds.ToString() + " seconds | Word count: " + wordCount.ToString() + " | Confidence: " + Math.Round(keywordExtraction.Confidence, 2).ToString() + " | Hit any key to continue", ConsoleColor.Green);
                _ = Console.ReadKey();
            }
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
    }
}
