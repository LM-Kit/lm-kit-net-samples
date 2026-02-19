using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat_with_yes_no_assistant
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

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 20,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = @"You are a fact-checking chatbot. Respond only with yes or no.",
                Grammar = new Grammar(Grammar.PredefinedGrammar.Boolean)
            };

            chat.AfterTextCompletion += OnAfterTextCompletion!;

            string prompt = "Hello chatbot, are you functioning properly?";

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();

                if (prompt == "")
                {
                    prompt = Console.ReadLine() ?? "";

                    if (string.IsNullOrWhiteSpace(prompt))
                        break;
                }
                else
                {
                    Console.WriteLine(prompt);
                }

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    prompt = "Hello chatbot, are you functioning properly?";
                    continue;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();
                TextGenerationResult result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                prompt = "";

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
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
