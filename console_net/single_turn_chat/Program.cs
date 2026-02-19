using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace single_turn_chat
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
            Console.WriteLine("=== Single-Turn Chat Demo ===\n");
            Console.WriteLine("Select a model:\n");
            Console.WriteLine("  0 - Google Gemma 3 4B           (~4 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen 3 8B           (~6 GB VRAM)");
            Console.WriteLine("  2 - Google Gemma 3 12B           (~9 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.Write("\n  Or enter a custom model URI\n\n> ");

            string input = Console.ReadLine()?.Trim() ?? "";

            LM model = LoadModel(input);

            Console.Clear();
            SingleTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling() { Temperature = 0.8f },
                SystemPrompt = "You are a chatbot that always responds promptly and helpfully to user requests."
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;

            string prompt = "Hello!";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                TextGenerationResult result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"\n[tokens: {result.GeneratedTokens.Count} | stop: {result.TerminationReason} | speed: {result.TokenGenerationRate:F1} tok/s | ctx: {result.ContextTokens.Count}/{result.ContextSize}]");
                Console.ResetColor();

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? "";
            }

            Console.WriteLine("Chat ended. Press any key to exit.");
            Console.ReadKey();
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
            {
                return LM.LoadFromModelID(modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(new Uri(input.Trim('"')),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
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
    }
}
