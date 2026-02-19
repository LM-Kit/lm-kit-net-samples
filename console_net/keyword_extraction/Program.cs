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
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4:14.7b",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3:0.6b",
                "7" => "llama3.2:1b",
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

        private static void Main(string[] args)
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
            Console.WriteLine("6 - Alibaba Qwen-3 0.6B (requires approximately 0.8 GB of VRAM)");
            Console.WriteLine("7 - Meta Llama 3.2 1B (requires approximately 1 GB of VRAM)");
            Console.Write("Other: Custom model URI\n\n> ");

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
