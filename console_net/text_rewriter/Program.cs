using LMKit.Model;
using LMKit.TextEnhancement;
using System.Text;

namespace text_rewriter
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA3_4B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-3-4b-instruct-lmk/resolve/main/gemma-3-4b-it-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-3-8b-instruct-gguf/resolve/main/Qwen3-8B-Q4_K_M.gguf";
        static readonly string DEFAULT_MINISTRAL_3_8_MODEL_PATH = @"https://huggingface.co/lm-kit/ministral-3-3b-instruct-lmk/resolve/main/ministral-3-3b-instruct-Q4_K_M.lmk";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GRANITE_4_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/granite-4.0-h-tiny-gguf/resolve/main/Granite-4.0-H-Tiny-64x994M-Q4_K_M.gguf";
        static readonly string DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH = @"https://huggingface.co/lm-kit/gpt-oss-20b-gguf/resolve/main/gpt-oss-20b-mxfp4.gguf";
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
            switch (e.SegmentType)
            {
                case LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning:
                    Console.ForegroundColor = ConsoleColor.Blue;
                    break;
                case LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;
                case LMKit.TextGeneration.Chat.TextSegmentType.UserVisible:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;
            }

            Console.Write(e.Text);
        }

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            var language = LMKit.TextGeneration.Language.English; //set end user language here.

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Mistral Ministral 3 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 4B Medium (requires approximately 4 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Microsoft Phi-4 14.7B Mini (requires approximately 11 GB of VRAM)");
            Console.WriteLine("6 - IBM Granite 4 7B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("7 - Open AI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = DEFAULT_MINISTRAL_3_8_MODEL_PATH;
                    break;
                case "1":
                    modelLink = DEFAULT_LLAMA3_1_8B_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_GEMMA3_4B_MODEL_PATH;
                    break;
                case "3":
                    modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN3_8B_MODEL_PATH;
                    break;
                case "5":
                    modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH;
                    break;
                case "6":
                    modelLink = DEFAULT_GRANITE_4_7B_MODEL_PATH;
                    break;
                case "7":
                    modelLink = DEFAULT_OPENAI_GPT_OSS_20B_MODEL_PATH;
                    break;
                default:
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new(modelLink);
            LM model = new(modelUri,
                                    downloadingProgress: ModelDownloadingProgress,
                                    loadingProgress: ModelLoadingProgress);

            Console.Clear();
            TextRewriter rewriter = new(model);

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