using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace multi_turn_chat_with_persistent_session
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI4_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-mini-3.8b-instruct-gguf/resolve/main/Phi-4-mini-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_QWEN2_5_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2.5-7b-instruct-gguf/resolve/main/Qwen-2.5-7B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf?download=true";
        static readonly string DEFAULT_PHI4_14_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-4-14.7b-instruct-gguf/resolve/main/Phi-4-14.7B-Instruct-Q4_K_M.gguf?download=true";
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
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (requires approximately 7 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 Mini 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2.5 7.6B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("5 - Microsoft Phi-4 14.7B Mini (requires approximately 11 GB of VRAM)");
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
                    modelLink = DEFAULT_PHI4_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN2_5_7B_MODEL_PATH;
                    break;
                case "5":
                    modelLink = DEFAULT_PHI4_14_7B_MODEL_PATH;
                    break;
                default:
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(modelUri,
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