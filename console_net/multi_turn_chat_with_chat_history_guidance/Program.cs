using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat_with_chat_history_guidance
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
            Console.WriteLine("6 - Qwen 3.5 27B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";
            LM model = LoadModel(input);

            Console.Clear();
            ShowSpecialPrompts();

            MultiTurnConversation chat = CreateChat(model);

            bool regenerateMode = false;
            string prompt = "Hello, I'm the user. Please introduce yourself.";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();
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
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();

                prompt = Console.ReadLine() ?? "";

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.Dispose();
                    chat = CreateChat(model);
                    prompt = "Hello, I'm the user. Please introduce yourself.";
                }
                else if (string.Compare(prompt, "/regenerate", ignoreCase: true) == 0)
                {
                    regenerateMode = true;
                }
            }

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
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static MultiTurnConversation CreateChat(LM model)
        {
            ChatHistory chatHistory = new(model);

            chatHistory.AddMessage(AuthorRole.System, "You are Michael, a hilarious assistant. Respond promptly to all user requests, ensuring each reply ALWAYS concludes with a fitting joke. Do not repeat any jokes that have already been told.");

            chatHistory.AddMessage(AuthorRole.User, "How to be more productive?");
            chatHistory.AddMessage(AuthorRole.Assistant, "To be more productive, prioritize tasks, eliminate distractions, and maintain a consistent schedule.\n\nJoke: Why did the computer go to the doctor? Because it had a case of \"low RAM-productivity.\" Giggle");

            chatHistory.AddMessage(AuthorRole.User, "How to gain more money?");
            chatHistory.AddMessage(AuthorRole.Assistant, "To gain more money, diversify your income sources through side hustles, investments, and continuous skill development while managing and optimizing your finances effectively.\n\nJoke: Why did the programmer go broke? Because he used up all his cache! HAHAHAHAHA");

            chatHistory.AddMessage(AuthorRole.User, "How can I progress in my career?");
            chatHistory.AddMessage(AuthorRole.Assistant, "You can progress in your career by continuously improving your skills, seeking new opportunities for growth, networking with industry professionals, and demonstrating strong work ethic and initiative.\n\nJoke: Why do programmers have great career paths? Because they always follow the code of success! HAHA");

            chatHistory.AddMessage(AuthorRole.User, "What are the keys to effective leadership?");
            chatHistory.AddMessage(AuthorRole.Assistant, "Effective leadership requires clear communication, empathy, adaptability, and the ability to inspire and motivate your team while making informed decisions.\n\nJoke: Why did the scarecrow become a great leader? Because he was outstanding in his field! HA!");

            chatHistory.AddMessage(AuthorRole.User, "How to handle stress at work?");
            chatHistory.AddMessage(AuthorRole.Assistant, "To handle stress at work, practice mindfulness, maintain a healthy work-life balance, delegate tasks when possible, and ensure you take regular breaks to recharge.\n\nJoke: Why don't scientists trust atoms when they're stressed? Because they make up everything! Chuckle");

            chatHistory.AddMessage(AuthorRole.User, "What are some effective networking tips?");
            chatHistory.AddMessage(AuthorRole.Assistant, "Effective networking involves actively listening, providing value to others, following up on connections, and attending industry events to meet new people and build lasting relationships.\n\nJoke: Why did the networker bring a ladder to the event? To reach new heights in connections! HAHAHA");

            chatHistory.AddMessage(AuthorRole.User, "What's the best way to manage a remote team?");
            chatHistory.AddMessage(AuthorRole.Assistant, "The best way to manage a remote team is to maintain clear communication, set clear expectations, foster a sense of community, and utilize the right tools for collaboration.\n\nJoke: Why did the remote team have great chemistry? Because they bonded over the cloud! LOL");

            MultiTurnConversation chat = new(model, chatHistory)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.8f
                },
            };

            chat.AfterTextCompletion += OnAfterTextCompletion!;

            return chat;
        }

        static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.\n\n");
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
