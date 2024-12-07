﻿using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace structured_data_extraction
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI3_5_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-3.5-mini-3.8b-instruct-gguf/resolve/main/Phi-3.5-mini-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN2_5_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2.5-7b-instruct-gguf/resolve/main/Qwen-2.5-7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN2_5_05B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2.5-0.5b-instruct-gguf/resolve/main/Qwen-2.5-0.5B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_LLAMA_3_2_1B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.2-1b-instruct.gguf/resolve/main/Llama-3.2-1B-Instruct-Q4_K_M.gguf?download=true";
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

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(""); //set an optional license key here if available.
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (requires approximately 7 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-3.5 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2.5 7.6B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Alibaba Qwen-2.5 0.5.6B (requires approximately 0.8 GB of VRAM)");
            Console.WriteLine("6 - Meta Llama 3.2 1B (requires approximately 1 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

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
                    modelLink = input.Trim().Trim('"'); ;
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LLM model = new LLM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            KeywordExtraction keywordExtraction = new KeywordExtraction(model)
            {
                KeywordCount = 8
            };

            while (true)
            {
                Console.Clear();
                Console.WriteLine("Please enter the path to the text file containing your input:\n");
                Console.Write("\n> ");
                string inputFilePath = Console.ReadLine().Trim().Trim('"');

                if (!File.Exists(inputFilePath))
                {
                    WriteColor("invalid file path. Hit any key to retry.", ConsoleColor.Red);
                    Console.ReadKey();
                    continue;
                }

                Console.Clear();

                string content = File.ReadAllText(inputFilePath);

                Console.WriteLine("\n\nExtracting keywords...\n");
                Stopwatch sw = Stopwatch.StartNew();
                var keywords = keywordExtraction.ExtractKeywords(content);
                sw.Stop();

                WriteColor("\nExtracted elements:\n", ConsoleColor.Green);

                foreach (var item in keywords)
                {
                    Console.WriteLine($"{item.Value}");
                }

                WriteColor("\nExtraction done in " + sw.Elapsed.TotalSeconds.ToString() + " seconds | Confidence: " + Math.Round(keywordExtraction.Confidence, 2).ToString() + " | Hit any key to continue", ConsoleColor.Green);
                Console.ReadKey();
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