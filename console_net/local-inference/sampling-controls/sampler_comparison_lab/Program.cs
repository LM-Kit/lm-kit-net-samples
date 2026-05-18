using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace sampler_comparison_lab
{
    internal class Program
    {
        static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading qwen3.5:4b ...");
            using LM model = LM.LoadFromModelID("qwen3.5:4b",
                downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            Console.WriteLine();
            Console.WriteLine();

            PrintMenu();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "compare":
                        CompareAllSamplers(model);
                        break;
                    case "2": case "greedy":
                        OneSampler(model, "GREEDY", new GreedyDecoding());
                        break;
                    case "3": case "lowtemp":
                        OneSampler(model, "RANDOM low-temp tight top-p", new RandomSampling
                        { Temperature = 0.3f, TopP = 0.85f, MinP = 0.05f, TopK = 40 });
                        break;
                    case "4": case "hightemp":
                        OneSampler(model, "RANDOM high-temp wide top-p", new RandomSampling
                        { Temperature = 0.95f, TopP = 0.98f, MinP = 0.02f, TopK = 100 });
                        break;
                    case "5": case "mirostat":
                        OneSampler(model, "MIROSTAT v2", new Mirostat2Sampling
                        { Temperature = 0.8f, TargetEntropy = 5.0f, LearningRate = 0.1f });
                        break;
                    case "6": case "custom":
                        OneSampler(model, "CUSTOM random", PromptCustomRandom());
                        break;
                    case "q": case "quit": case "exit":
                        return;
                    case "?": case "help": case "menu":
                        PrintMenu();
                        break;
                    default:
                        Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                        break;
                }
            }
        }

        static void CompareAllSamplers(LM model)
        {
            string prompt = PromptOrDefault();
            OneSampler(model, "GREEDY", new GreedyDecoding(), prompt);
            OneSampler(model, "RANDOM low-temp tight top-p", new RandomSampling
            { Temperature = 0.3f, TopP = 0.85f, MinP = 0.05f, TopK = 40 }, prompt);
            OneSampler(model, "RANDOM high-temp wide top-p", new RandomSampling
            { Temperature = 0.95f, TopP = 0.98f, MinP = 0.02f, TopK = 100 }, prompt);
            OneSampler(model, "MIROSTAT v2", new Mirostat2Sampling
            { Temperature = 0.8f, TargetEntropy = 5.0f, LearningRate = 0.1f }, prompt);
        }

        static void OneSampler(LM model, string label, TokenSampling sampler, string? prompt = null)
        {
            prompt ??= PromptOrDefault();
            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 200,
                SamplingMode = sampler,
                SystemPrompt = "You write tight, punchy prose. No fluff.",
            };
            chat.RepetitionPenalty.TokenCount = 64;
            chat.RepetitionPenalty.RepeatPenalty = 1.1f;
            chat.RepetitionPenalty.FrequencyPenalty = 0.0f;
            chat.RepetitionPenalty.PresencePenalty = 0.0f;

            Console.WriteLine();
            Console.WriteLine($"---- {label} ----");
            chat.AfterTextCompletion += (_, e) => Console.Write(e.Text);
            TextGenerationResult result = chat.Submit(prompt);
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine($"  tokens: {result.GeneratedTokens.Count,4}   speed: {result.TokenGenerationRate,6:F1} tok/s   stop: {result.TerminationReason}");
        }

        static string PromptOrDefault()
        {
            Console.Write("Prompt (blank = default): ");
            string? p = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(p))
            {
                return "Write a short, two-sentence elevator pitch for a developer SDK that runs open-weight LLMs on the user's own hardware.";
            }
            return p;
        }

        static RandomSampling PromptCustomRandom()
        {
            Console.Write("Temperature (default 0.7): ");
            float.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float temp);
            if (temp <= 0) { temp = 0.7f; }
            Console.Write("TopP (default 0.9): ");
            float.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float topP);
            if (topP <= 0) { topP = 0.9f; }
            Console.Write("MinP (default 0.05): ");
            float.TryParse(Console.ReadLine(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float minP);
            if (minP < 0) { minP = 0.05f; }
            Console.Write("TopK (default 40): ");
            int.TryParse(Console.ReadLine(), out int topK);
            if (topK <= 0) { topK = 40; }

            return new RandomSampling { Temperature = temp, TopP = topP, MinP = minP, TopK = topK };
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue) { Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%"); }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Sampler Comparison Lab                      ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Run the same prompt under Greedy, low-temp Random, high-temp Random, and Mirostat v2.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / compare    Run one prompt under all 4 standard samplers");
            Console.WriteLine("  2 / greedy     Greedy decoding only");
            Console.WriteLine("  3 / lowtemp    Random sampling, low temp + tight top-p");
            Console.WriteLine("  4 / hightemp   Random sampling, high temp + wide top-p");
            Console.WriteLine("  5 / mirostat   Mirostat v2 entropy-controlled");
            Console.WriteLine("  6 / custom     Random sampling with user-typed Temp/TopP/MinP/TopK");
            Console.WriteLine("  q / quit       Exit");
            Console.WriteLine();
        }
    }
}
