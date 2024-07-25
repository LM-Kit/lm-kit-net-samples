using LMKit.Model;
using LMKit.Exceptions;
using LMKit.Inference;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace multi_turn_chat_with_coding_assistant
{
    internal class Program
    {
        static readonly string DEFAULT_SMALL_MODEL_PATH = @"https://huggingface.co/lm-kit/deepseek-coder-1.6-7b-gguf/resolve/main/DeepSeek-Coder-1.6-7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_MEDIUM_MODEL_PATH = @"https://huggingface.co/lm-kit/deepseek-coder-2-lite-15.7b-gguf/resolve/main/DeepSeek-Coder-2-Lite-15.7B-Instruct-Q4_K_M.gguf";
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
            Console.WriteLine("1 - DeepSeek V1 Small (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - DeepSeek V2 Medium (requires approximately 12 GB of VRAM)");
            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "1":
                    modelLink = DEFAULT_SMALL_MODEL_PATH;
                    break;
                case "2":
                    modelLink = DEFAULT_MEDIUM_MODEL_PATH;
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
            ShowSpecialPrompts();
            MultiTurnConversation chat = new MultiTurnConversation(model, contextSize: 4096)
            {
                MaximumCompletionTokens = int.MaxValue,
                SamplingMode = new RandomSampling(),
                SystemPrompt = "",  // I don't believe we need to set special system instructions with this coding model.
            };

            chat.InferencePolicies.InputLengthOverflowPolicy = InputLengthOverflowPolicy.Throw;

            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            bool regenerateMode = false;
            string prompt = "Hello! 1+1==1, right?";

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write($"User: ");
            Console.ResetColor();
            Console.WriteLine(prompt);

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();
                try
                {
                    TextGenerationResult result;

                    if (regenerateMode)
                    {
                        result = chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                        regenerateMode = false;
                    }
                    else
                    {
                        result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                    }

                    Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
                }
                catch (NotEnoughContextSizeException e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"The prompt length of {e.RequiredTokens} exceeds the context size of {e.ContextSize}. Please increase the context size specified around line 52 of this demo to perform this operation.");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine();

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    prompt = "Hello!";
                }
                else if (string.Compare(prompt, "/regenerate", ignoreCase: true) == 0)
                {
                    regenerateMode = true;
                }
                else if (prompt.Trim().StartsWith("/analyse", StringComparison.OrdinalIgnoreCase))
                {
                    chat.ClearHistory();
                    string filePath = prompt.Trim().Substring(8).Trim().Trim('"');

                    prompt = "Analyze the code below and provide insights about its purpose:\n";

                    if (File.Exists(filePath))
                    {
                        prompt += File.ReadAllText(filePath);
                    }
                    else
                    {
                        throw new FileNotFoundException(filePath);
                    }
                }
                else if (prompt.Trim().StartsWith("/reviewcomments", StringComparison.OrdinalIgnoreCase))
                {
                    chat.ClearHistory();
                    string filePath = prompt.Trim().Substring(15).Trim().Trim('"');

                    prompt = "Revise all the following code to enhance the comments and documentation for better clarity and understanding:\n";

                    if (File.Exists(filePath))
                    {
                        prompt += File.ReadAllText(filePath);
                    }
                    else
                    {
                        throw new FileNotFoundException(filePath);
                    }
                }
            }

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a new session.");
            Console.WriteLine("Use '/regenerate' to get a new completion from the last input.");
            Console.WriteLine("Use '/analyse [PATH]' to analyze a code file from the specified path.");
            Console.WriteLine("Use '/reviewcomments [PATH]' to review and improve code comments from the specified path.\n\n");
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}