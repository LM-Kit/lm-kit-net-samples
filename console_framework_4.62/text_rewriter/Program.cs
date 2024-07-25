using LMKit.Model;
using LMKit.TextEnhancement;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace text_rewriter
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3-8b-instruct-gguf/resolve/main/Llama-3-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI3_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-3-instruct-gguf/resolve/main/Phi-3.1-mini-4k-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN2_7_6B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2-7.6b-instruct-gguf/resolve/main/Qwen-2-7.6B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf";
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

        private static void Rewrite_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }

        static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(""); //set an optional license key here if available.
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            var language = LMKit.TextGeneration.Language.English; //set end user language here.

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (requires approximately 7 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-3 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2 7.6B (requires approximately 5.6 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH;
                    break;
                case "1":
                    modelLink = DEFAULT_LLAMA3_8B_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_GEMMA2_9B_MODEL_PATH;
                    break;
                case "3":
                    modelLink = DEFAULT_PHI3_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN2_7_6B_MODEL_PATH;
                    break;
                default:
                    modelLink = input.Trim().Trim('"');;
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LLM model = new LLM(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            TextRewriter rewriter = new TextRewriter(model);

            rewriter.AfterTextCompletion += Rewrite_AfterTextCompletion;
            int correctionCount = 0;

            while (true)
            {
                if (correctionCount > 0)
                {
                    Console.Write("\n\n");
                }

                WriteLineColor($"Enter text to be rewritten, or type 'exit' to quit the app:\n", ConsoleColor.Green);

                string text = Console.ReadLine();

                while (string.IsNullOrWhiteSpace(text))
                {
                    text = Console.ReadLine();
                }

                if (text == "exit")
                {
                    return;
                }

                WriteLineColor($"\nSelect communication style:\n1 - Concise\n2 - Professional\n3 - Friendly\n4 - All styles", ConsoleColor.Green);

                char keyChar = Console.ReadKey(true).KeyChar;

                if (keyChar == 52)
                {//all

                    foreach (var style in Enum.GetValues(typeof(TextRewriter.CommunicationStyle)))
                    {
                        WriteLineColor($"\n\n>> Rewriting the text with {style.ToString().ToLowerInvariant()} style...\n", ConsoleColor.Blue);

                        _ = rewriter.Rewrite(text, (TextRewriter.CommunicationStyle)style, language, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    }
                }
                else
                {
                    TextRewriter.CommunicationStyle style;

                    switch ((int)keyChar)
                    {
                        case 49:
                            style = TextRewriter.CommunicationStyle.Concise;
                            break;
                        case 50:
                        default:
                            style = TextRewriter.CommunicationStyle.Professional;
                            break;
                        case 51:
                            style = TextRewriter.CommunicationStyle.Friendly;
                            break;
                    }

                    WriteLineColor($"\n>> Rewriting the text with {style.ToString().ToLowerInvariant()} style...\n", ConsoleColor.Blue);

                    _ = rewriter.Rewrite(text, style, language, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                }

                correctionCount++;
            }
        }

        private static void WriteLineColor(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
        }
    }
}