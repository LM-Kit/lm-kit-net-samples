using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using System.Text;

namespace multi_turn_chat_with_persistent_session
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
            Console.WriteLine("0 - Gemma 3 4B (requires approximately 5.7 GB of VRAM)");
            Console.WriteLine("1 - Qwen 3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("2 - Gemma 3 12B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("3 - Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - GLM 4.7 Flash (requires approximately 18 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            Console.Clear();
            ShowSpecialPrompts();

            MultiTurnConversation chat;
            string sessionPath = "session" + model.Name + ".bin";

            if (File.Exists(sessionPath))
            {
                chat = new MultiTurnConversation(model, sessionPath);
                chat.AfterTextCompletion += OnAfterTextCompletion;

                foreach (var message in chat.ChatHistory.Messages)
                {
                    if (message.AuthorRole == AuthorRole.System)
                        continue;
                    else if (message.AuthorRole == AuthorRole.Assistant)
                    {
                        WriteColor("\nAssistant: ", ConsoleColor.Green, addNL: false);
                        Console.Write(message.Text);
                    }
                    else if (message.AuthorRole == AuthorRole.User)
                    {
                        WriteColor("\n\nUser: ", ConsoleColor.Green, addNL: false);
                        Console.Write(message.Text);
                    }
                }
            }
            else
            {
                chat = new MultiTurnConversation(model);
                chat.AfterTextCompletion += OnAfterTextCompletion;
                WriteColor("Assistant: ", ConsoleColor.Green, addNL: false);
                _ = chat.Submit("hello!");
            }

            while (true)
            {
                WriteColor($"\n\nUser: ", ConsoleColor.Green, addNL: false);

                bool regenerateMode = false;
                string? prompt = Console.ReadLine();

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

            WriteColor($"\nSaving session...", ConsoleColor.Blue);
            chat.SaveSession("session" + model.Name + ".bin");
            WriteColor($"Session saved!", ConsoleColor.Blue);

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.");
            Console.WriteLine("Use an empty prompt to save the chat session and exit.\n\n");
        }

        static void OnAfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
        }

        static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
                Console.WriteLine(text);
            else
                Console.Write(text);
            Console.ResetColor();
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
