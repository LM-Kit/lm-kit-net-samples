using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using System.Text;
using tool_calling_assistant.Tools;

namespace tool_calling_assistant
{
    internal class Program
    {
        static bool _isDownloading;

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

        static LM LoadModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3:8b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        private static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "");

            Console.Clear();
            ShowSpecialPrompts();

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.8f
                }
            };

            chat.Tools.Register(new CurrencyTool());
            chat.Tools.Register(new WeatherTool());
            chat.Tools.Register(new UnitConversionTool());

            chat.AfterTextCompletion += OnAfterTextCompletion;

            const string FIRST_MESSAGE = "Greet the user, then list available tools; do not describe JSON schemas.";
            string mode = "chat";
            string prompt = FIRST_MESSAGE;

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

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    prompt = FIRST_MESSAGE;
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

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
            Console.WriteLine("Use '/continue' to continue last assistant message.");
            Console.WriteLine("Use '/regenerate' to obtain a new completion from the last input.\n\n");
        }

        private static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
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
