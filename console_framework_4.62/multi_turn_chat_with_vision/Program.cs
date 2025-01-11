using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System;
using System.Text;
using System.Threading;

namespace multi_turn_chat_with_vision
{
    internal class Program
    {
        static readonly string DEFAULT_MINI_CPM_2_6_MODEL_PATH = @"https://huggingface.co/lm-kit/minicpm-2.6-lmk/resolve/main/MiniCPM-V-2.6-Q4_K_M.lmk?download=true";
        static readonly string DEFAULT_QWEN_2_VL_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen2-vl-2b-instruct-lmk/resolve/main/Qwen2-VL-2B-Instruct-Q4_K_M.lmk?download=true";
        static readonly string DEFAULT_QWEN_2_VL_8_3B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen2-vl-8.3b-instruct-lmk/resolve/main/Qwen2-VL-8.3B-Instruct-Q4_K_M.lmk?download=true";

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
            Console.WriteLine("0 - MiniCPM 2.6 Vision 8.1B (requires approximately 6.8 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 2 Vision 2.2B (requires approximately 3 GB of VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 2 Vision 8.3B (requires approximately 7.3 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = DEFAULT_MINI_CPM_2_6_MODEL_PATH;
                    break;
                case "1":
                    modelLink = DEFAULT_QWEN_2_VL_2B_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_QWEN_2_VL_8_3B_MODEL_PATH;
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
            MultiTurnConversation chat = new MultiTurnConversation(model)
            {
                MaximumCompletionTokens = 1000,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.1f //note: lower temperature is better for vision models.
                },
                SystemPrompt = "You are a chatbot that always responds promptly and helpfully to user requests."
            };


            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            string mode = "start_new_chat";
            string prompt = "";

            while (true)
            {
                Attachment attachment = null;

                if (mode == "start_new_chat")
                {
                    while (true)
                    {
                        Console.Write("Enter the path to an image attachment:\n\n> ");
                        string path = Console.ReadLine();
                        try
                        {
                            attachment = new Attachment(path);
                            Console.WriteLine("");
                            break;
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message} Please check the file path and permissions.");
                            Console.ResetColor();
                        }
                    }
                    prompt = "describe the image";
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();
                TextGenerationResult result;

                if (mode == "regenerate")
                {
                    result = chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    mode = "chat";
                }
                else if (mode == "continue")
                {
                    result = chat.ContinueLastAssistantResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    mode = "chat";
                }
                else
                {
                    if (attachment != null)
                    {
                        result = chat.Submit(new Prompt(prompt, attachment), new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    }
                    else
                    {
                        result = chat.Submit(new Prompt(prompt), new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                    }

                    mode = "chat";
                }

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    break;
                }
                else if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    mode = "start_new_chat";
                }
                else if (string.Compare(prompt, "/regenerate", ignoreCase: true) == 0)
                {
                    mode = "regenerate";
                }
                else if (string.Compare(prompt, "/continue", ignoreCase: true) == 0)
                {
                    mode = "continue";
                }
            }

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/continue' to continue last assistant message.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.\n\n");
        }
        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}