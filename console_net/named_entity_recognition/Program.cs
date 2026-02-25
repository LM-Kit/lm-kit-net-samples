using LMKit.Data;
using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace named_entity_recognition
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - MiniCPM o 4.5 (requires approximately 5.9 GB of VRAM)");
            Console.WriteLine("1 - Qwen 3 VL 2B (requires approximately 2.5 GB of VRAM)");
            Console.WriteLine("2 - Qwen 3 VL 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Qwen 3 VL 8B (requires approximately 6.5 GB of VRAM)");
            Console.WriteLine("4 - Gemma 3 4B (requires approximately 5.7 GB of VRAM)");
            Console.WriteLine("5 - Gemma 3 12B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("6 - Alibaba Qwen 3.5 27B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            Console.Clear();
            NamedEntityRecognition engine = new(model);

            while (true)
            {
                Console.Write("Please enter the path to an image, document or text file:\n\n> ");
                string? path = Console.ReadLine();

                if (string.IsNullOrEmpty(path))
                    break;

                try
                {
                    Stopwatch sw = Stopwatch.StartNew();
                    var attachment = new Attachment(path);
                    var entities = engine.Recognize(attachment);
                    sw.Stop();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"{entities.Count} detected entities | processing time: {sw.Elapsed}\n");
                    Console.ResetColor();

                    foreach (var entity in entities)
                    {
                        Console.WriteLine($"{entity.EntityDefinition.Label}: \"{entity.Value}\" (confidence={entity.Confidence:0.##})");
                    }

                    Console.WriteLine("\n\n");
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

        static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "minicpm-o-45",
                "1" => "qwen3-vl:2b",
                "2" => "qwen3-vl:4b",
                "3" => "qwen3-vl:8b",
                "4" => "gemma3:4b",
                "5" => "gemma3:12b",
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
            Console.Write(contentLength.HasValue
                ? $"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%"
                : $"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
