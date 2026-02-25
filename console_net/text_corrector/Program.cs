using LMKit.Model;
using LMKit.TextEnhancement;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace text_corrector
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
            Console.WriteLine("=== Text Corrector Demo ===\n");
            Console.WriteLine("Select a model:\n");
            Console.WriteLine("  0 - Google Gemma 3 4B           (~4 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen 3 8B           (~6 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B           (~9 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.5 27B         (~18 GB VRAM)");
            Console.Write("\n  Or enter a custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            Console.Clear();
            TextCorrection corrector = new(model);
            corrector.AfterTextCompletion += OnAfterTextCompletion;
            int correctionCount = 0;

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;

                if (correctionCount > 0)
                {
                    Console.Write("\n\n");
                }

                Console.Write("Enter a text to be corrected:\n\n");
                Console.ResetColor();

                string? text = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(text))
                {
                    break;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nCorrecting the text...\n");
                Console.ResetColor();
                _ = corrector.Correct(text, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                correctionCount++;
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
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
                "6" => "qwen3.5:27b",
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

        static void OnAfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
        }
    }
}
