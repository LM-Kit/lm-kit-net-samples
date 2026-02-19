using LMKit.Data;
using LMKit.Model;
using LMKit.TextGeneration;
using System.Diagnostics;
using System.Text;

namespace text_summarizer_from_document
{
    internal class Program
    {
        private static bool _isDownloading;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - MiniCPM o 4.5 9B       (~5.9 GB VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3 VL 2B   (~2.5 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3 VL 4B   (~4.5 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 VL 8B   (~6.5 GB VRAM)");
            Console.WriteLine("4 - Google Gemma 3 4B       (~5.7 GB VRAM)");
            Console.WriteLine("5 - Google Gemma 3 12B      (~11 GB VRAM)");
            Console.Write("\nOther entry: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "0";
            LM model = LoadModel(input);

            Console.Clear();
            Summarizer summarizer = new(model)
            {
                GenerateTitle = true,
                GenerateContent = true,
                MaxContentWords = 100,
                Guidance = ""
            };

            while (true)
            {
                Console.Write("Enter the path to a document:\n\n> ");
                string? path = Console.ReadLine();

                if (string.IsNullOrEmpty(path))
                {
                    break;
                }

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    var attachment = new Attachment(path);
                    var result = summarizer.Summarize(attachment);
                    sw.Stop();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Title: {result.Title}");
                    Console.WriteLine($"Summary: {result.Summary}");
                    Console.ResetColor();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "minicpm-o-45",
                "1" => "qwen3-vl:2b",
                "2" => "qwen3-vl:4b",
                "3" => "qwen3-vl:8b",
                "4" => "gemma3:4b",
                "5" => "gemma3:12b",
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
