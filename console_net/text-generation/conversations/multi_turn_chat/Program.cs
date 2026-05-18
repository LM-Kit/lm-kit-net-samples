using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Multi-Turn Chat Demo ===\n");
            Console.WriteLine("Select a model:\n");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 9B          (~7 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 4 E4B           (~6 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.6 27B         (~18 GB VRAM)");
            Console.Write("\n  Or enter a custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.8f
                },
                SystemPrompt = "You are a chatbot that always responds promptly and helpfully to user requests."
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;

            Console.Clear();
            ShowSpecialPrompts(chat);

            string mode = "chat";
            string prompt = "Hello!";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
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
                    result = chat.Submit(
                        prompt,
                        new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                }

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write($"\n[tokens: {result.GeneratedTokens.Count} | stop: {result.TerminationReason} | speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s | ctx: {result.ContextTokens.Count}/{result.ContextSize}]");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine()!;

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    prompt = "Hello!";
                }
                else if (string.Compare(prompt, "/think", ignoreCase: true) == 0)
                {
                    if (chat.ReasoningLevel != ReasoningLevel.None)
                    {
                        chat.ClearHistory();
                        chat.ReasoningLevel = ReasoningLevel.None;
                    }
                    else
                    {
                        chat.ClearHistory();
                        chat.ReasoningLevel = ReasoningLevel.Medium;
                    }

                    string state = chat.ReasoningLevel != ReasoningLevel.None ? "ON" : "OFF";
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\nReasoning toggled {state}. History cleared.\n");
                    Console.ResetColor();

                    prompt = "Hello!";
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

            Console.WriteLine("Chat ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        static LM LoadModel(string input)
        {
            string? modelId = input switch
            {
                "1" => "qwen3.5:9b",
                "2" => "gemma4:e4b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.6:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static void OnAfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
        }

        static void ShowSpecialPrompts(MultiTurnConversation chat)
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/continue' to continue last assistant message.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.");
            Console.WriteLine("Use '/think' to toggle reasoning on/off.");

            if (chat != null)
            {
                string state = chat.ReasoningLevel != ReasoningLevel.None ? "ON" : "OFF";
                Console.WriteLine($"\nReasoning: {state}");
            }

            Console.WriteLine();
        }
    }
}
