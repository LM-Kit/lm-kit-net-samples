using LMKit.Data;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace document_to_markdown
{
    internal class Program
    {
        private static bool _isDownloading;

        private static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - LightOn LightOnOCR 2 1B    (~2 GB VRAM) (recommended)");
            Console.WriteLine("1 - Z.ai GLM-OCR 0.9B          (~1 GB VRAM)");
            Console.WriteLine("2 - Z.ai GLM-V 4.6 Flash 10B   (~7 GB VRAM)");
            Console.WriteLine("3 - MiniCPM o 4.5 9B           (~5.9 GB VRAM)");
            Console.WriteLine("4 - Alibaba Qwen 3 VL 2B       (~2.5 GB VRAM)");
            Console.WriteLine("5 - Alibaba Qwen 3 VL 4B       (~4.5 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen 3 VL 8B       (~6.5 GB VRAM)");
            Console.WriteLine("7 - Google Gemma 3 4B           (~5.7 GB VRAM)");
            Console.WriteLine("8 - Google Gemma 3 12B          (~11 GB VRAM)");
            Console.WriteLine("9 - Alibaba Qwen 3.5 27B        (~18 GB VRAM)");
            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            LM model = LoadModel(input);

            Console.Clear();

            VlmOcr ocr = new VlmOcr(model);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Vision OCR Demo");
            Console.ResetColor();

            while (true)
            {
                Attachment? attachment = null;

                while (true)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("Assistant");
                    Console.ResetColor();
                    Console.Write(" - enter image path (or 'q' to quit):\n> ");

                    string path = Console.ReadLine() ?? string.Empty;
                    path = path.Trim();

                    if (string.Equals(path, "q", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("\nDemo ended. Press any key to exit.");
                        Console.ReadKey();
                        return;
                    }

                    try
                    {
                        attachment = new Attachment(path);
                        Console.WriteLine();
                        break;
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\nError: Unable to open '{path}'.");
                        Console.WriteLine($"Details: {e.Message}");
                        Console.ResetColor();
                        Console.WriteLine("\nPlease check the file path and permissions, then try again.\n");
                    }
                }

                for (int pageIndex = 0; pageIndex < attachment.PageCount; pageIndex++)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine($"\n---------- Result page {pageIndex + 1}/{attachment.PageCount} ----------");
                    Console.ResetColor();

                    Stopwatch sw = Stopwatch.StartNew();
                    var result = ocr.Run(attachment, pageIndex);
                    sw.Stop();

                    Console.WriteLine(result.PageElement.Text);

                    double elapsedSeconds = sw.Elapsed.TotalSeconds;

                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.WriteLine("\n---------- Stats ----------");
                    Console.WriteLine($" elapsed time : {elapsedSeconds:F2} s");
                    Console.WriteLine($" gen. tokens  : {result.TextGeneration.GeneratedTokens.Count}");
                    Console.WriteLine($" stop reason  : {result.TextGeneration.TerminationReason}");
                    Console.WriteLine($" quality      : {Math.Round(result.TextGeneration.QualityScore, 2)}");
                    Console.WriteLine($" speed        : {Math.Round(result.TextGeneration.TokenGenerationRate, 2)} tok/s");
                    Console.WriteLine($" ctx usage    : {result.TextGeneration.ContextTokens.Count}/{result.TextGeneration.ContextSize}");
                    Console.ResetColor();

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine("----------------------------");
                    Console.ResetColor();
                }

                Console.Write("Press Enter to process another image, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    break;
                }

                Console.Clear();
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("LM-Kit Vision OCR Demo");
                Console.ResetColor();
                Console.WriteLine("Type the path to an image (or 'q' to quit).\n");
            }
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "lightonocr-2:1b",
                "1" => "glm-ocr",
                "2" => "glm-4.6v-flash",
                "3" => "minicpm-o-45",
                "4" => "qwen3-vl:2b",
                "5" => "qwen3-vl:4b",
                "6" => "qwen3-vl:8b",
                "7" => "gemma3:4b",
                "8" => "gemma3:12b",
                "9" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(input.Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double percent = (double)bytesRead / contentLength.Value * 100;
                Console.Write($"\rDownloading: {percent:F1}%   ");
            }
            else
            {
                Console.Write($"\rDownloading: {bytesRead / 1024.0 / 1024.0:F1} MB   ");
            }
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading: {progress * 100:F0}%   ");
            return true;
        }
    }
}
