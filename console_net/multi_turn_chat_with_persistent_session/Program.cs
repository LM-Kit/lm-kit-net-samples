using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace multi_turn_chat_with_persistent_session
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

        private static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

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
            ShowSpecialPrompts();

            MultiTurnConversation chat;

            string sessionPath = "session" + model.Name + ".bin";

            if (File.Exists(sessionPath))
            {
                chat = new MultiTurnConversation(model, sessionPath);

                chat.AfterTextCompletion += Chat_AfterTextCompletion;

                foreach (var message in chat.ChatHistory.Messages)
                {
                    if (message.AuthorRole == AuthorRole.System)
                    {
                        continue;
                    }
                    else if (message.AuthorRole == AuthorRole.Assistant)
                    {
                        WriteColor("\nAssistant: ", ConsoleColor.Green, addNL: false);
                        Console.Write(message.Content);
                    }
                    else if (message.AuthorRole == AuthorRole.User)
                    {
                        WriteColor("\n\nUser: ", ConsoleColor.Green, addNL: false);
                        Console.Write(message.Content);
                    }
                }
            }
            else
            {
                chat = new MultiTurnConversation(model);

                chat.AfterTextCompletion += Chat_AfterTextCompletion;
                WriteColor("Assistant: ", ConsoleColor.Green, addNL: false);
                _ = chat.Submit("hello!");
            }

            while (true)
            {
                WriteColor($"\n\nUser: ", ConsoleColor.Green, addNL: false);

                bool regenerateMode = false;
                string prompt = Console.ReadLine();

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    prompt = "hello!";
                }
                else if (string.Compare(prompt, "/regenerate", ignoreCase: true) == 0)
                {
                    regenerateMode = true;
                }
                else if (string.IsNullOrEmpty(prompt))
                {
                    break;
                }

                WriteColor("Assistant: ", ConsoleColor.Green, addNL: false);
                TextGenerationResult result;

                if (regenerateMode)
                {
                    result = chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    regenerateMode = false;
                }
                else
                {
                    result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                }

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
            }

            //saving session.
            WriteColor($"\nSaving session...", ConsoleColor.Blue);
            chat.SaveSession("session" + model.Name + ".bin");
            WriteColor($"Session saved!", ConsoleColor.Blue);

            Console.WriteLine("> The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.");
            Console.WriteLine("Use an empty prompt to save the chat session and exit.\n\n");
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
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