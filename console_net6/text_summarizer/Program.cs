using LMKit.Model;
using LMKit.TextGeneration;
using System.Diagnostics;
using System.Text;

namespace text_summarizer
{
    internal class Program
    {
        // Default model download paths
        private static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_PHI3_5_MINI_3_8B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/phi-3.5-mini-3.8b-instruct-gguf/resolve/main/Phi-3.5-mini-Instruct-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_QWEN2_5_7B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/qwen-2.5-7b-instruct-gguf/resolve/main/Qwen-2.5-7B-Instruct-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_QWEN2_5_05B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/qwen-2.5-0.5b-instruct-gguf/resolve/main/Qwen-2.5-0.5B-Instruct-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        private static readonly string DEFAULT_LLAMA_3_2_1B_MODEL_PATH =
            @"https://huggingface.co/lm-kit/llama-3.2-1b-instruct.gguf/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf?download=true";

        private static bool _isDownloading;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;

            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model... {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model... {bytesRead} bytes downloaded");
            }

            return true;
        }


        private static bool ModelLoadingProgress(float progress)
        {
            // Clear the console only once the download has completed.
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model... {Math.Round(progress * 100)}%");
            return true;
        }

        private static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();

            Console.WriteLine("Select the model you want to use:");
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (≈ 7.7 GB VRAM required)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (≈ 6 GB VRAM required)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (≈ 7 GB VRAM required)");
            Console.WriteLine("3 - Microsoft Phi-3.5 3.8B Mini (≈ 3.3 GB VRAM required)");
            Console.WriteLine("4 - Alibaba Qwen-2.5 7.6B (≈ 5.6 GB VRAM required)");
            Console.WriteLine("5 - Alibaba Qwen-2.5 0.5B (≈ 0.8 GB VRAM required)");
            Console.WriteLine("6 - Meta Llama 3.2 1B (≈ 1 GB VRAM required)");
            Console.Write("Or enter a custom model URI:\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH;
                    break;
                case "1":
                    modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_GEMMA2_9B_MODEL_PATH;
                    break;
                case "3":
                    modelLink = DEFAULT_PHI3_5_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN2_5_7B_MODEL_PATH;
                    break;
                case "5":
                    modelLink = DEFAULT_QWEN2_5_05B_MODEL_PATH;
                    break;
                case "6":
                    modelLink = DEFAULT_LLAMA_3_2_1B_MODEL_PATH;
                    break;
                default:
                    // If the user enters a custom URI
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            // Initialize the model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress
            );

            // Configure Summarizer
            Summarizer summarizer = new Summarizer(model)
            {
                GenerateContent = true,
                GenerateTitle = true,
                MaxContentWords = 100
            };

            // Main loop for repeated summarization tasks
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Please enter the path to the text file containing your input:");
                Console.Write("\n> ");
                string inputFilePath = Console.ReadLine().Trim().Trim('"');

                if (!File.Exists(inputFilePath))
                {
                    WriteColor("Invalid file path. Press any key to try again.", ConsoleColor.Red);
                    _ = Console.ReadKey();
                    continue;
                }

                Console.Clear();

                // Read and process the file content
                string content = File.ReadAllText(inputFilePath);
                int inputWordCount = content.Split(new[] { " ", "\r\n", "\n", "\t" }, StringSplitOptions.RemoveEmptyEntries).Length;

                WriteColor($"\nSummarizing content with {inputWordCount} words to {summarizer.MaxContentWords} max words...\n", ConsoleColor.Green);

                Stopwatch sw = Stopwatch.StartNew();
                var result = summarizer.Summarize(content);
                sw.Stop();

                // Display results
                WriteColor("Title:", ConsoleColor.Blue);
                Console.WriteLine(result.Title);
                WriteColor("Summary:", ConsoleColor.Blue);
                Console.WriteLine(result.Summary);

                int summaryWordCount = result.Summary.Split(new[] { " ", "\r\n", "\n", "\t" }, StringSplitOptions.RemoveEmptyEntries).Length;

                WriteColor(
                    $"\nSummarization completed in {sw.Elapsed.TotalSeconds:F2} seconds | " +
                    $"Summary word count: {summaryWordCount} | " +
                    $"Confidence: {Math.Round(result.Confidence, 2)}\nPress any key to continue.",
                    ConsoleColor.Green
                );

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