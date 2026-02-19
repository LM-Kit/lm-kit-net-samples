using LMKit.Model;
using LMKit.TextGeneration;
using System.Diagnostics;
using System.Text;

namespace text_summarizer
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Text Summarizer Demo ===\n");
            Console.WriteLine("Select a model:\n");
            Console.WriteLine("  0 - Google Gemma 3 4B           (~4 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen 3 8B           (~6 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B           (~9 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3 0.6B          (~0.8 GB VRAM)");
            Console.WriteLine("  7 - Meta Llama 3.2 1B            (~1 GB VRAM)");
            Console.Write("\n  Or enter a custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            Summarizer summarizer = new(model)
            {
                GenerateContent = true,
                GenerateTitle = true,
                MaxContentWords = 100
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Please enter the path to a text file:");
                Console.Write("\n> ");
                string inputFilePath = Console.ReadLine()?.Trim().Trim('"') ?? string.Empty;

                if (!File.Exists(inputFilePath))
                {
                    WriteColor("Invalid file path. Press any key to try again.", ConsoleColor.Red);
                    _ = Console.ReadKey();
                    continue;
                }

                Console.Clear();

                string content = File.ReadAllText(inputFilePath);
                int inputWordCount = content.Split([" ", "\r\n", "\n", "\t"], StringSplitOptions.RemoveEmptyEntries).Length;

                WriteColor($"\nSummarizing content with {inputWordCount} words to {summarizer.MaxContentWords} max words...\n", ConsoleColor.Green);

                Stopwatch sw = Stopwatch.StartNew();
                var result = summarizer.Summarize(content);
                sw.Stop();

                WriteColor("Title:", ConsoleColor.Blue);
                Console.WriteLine(result.Title);
                WriteColor("Summary:", ConsoleColor.Blue);
                Console.WriteLine(result.Summary);

                int summaryWordCount = result.Summary.Split([" ", "\r\n", "\n", "\t"], StringSplitOptions.RemoveEmptyEntries).Length;

                WriteColor(
                    $"\nSummarization completed in {sw.Elapsed.TotalSeconds:F2} seconds | " +
                    $"Summary word count: {summaryWordCount} | " +
                    $"Confidence: {Math.Round(result.Confidence, 2)}\nPress any key to continue.",
                    ConsoleColor.Green
                );

                _ = Console.ReadKey();
            }
        }

        static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3:0.6b",
                "7" => "llama3.2:1b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
                Console.WriteLine(text);
            else
                Console.Write(text);
            Console.ResetColor();
        }
    }
}
