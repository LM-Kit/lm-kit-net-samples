using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat_with_yes_no_assistant
{
    internal class Program
    {
        static readonly string DEFAULT_LLAMA3_1_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/llama-3.1-8b-instruct-gguf/resolve/main/Llama-3.1-8B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_GEMMA2_9B_MODEL_PATH = @"https://huggingface.co/lm-kit/gemma-2-9b-gguf/resolve/main/gemma-2-9B-Q4_K_M.gguf";
        static readonly string DEFAULT_PHI3_MINI_3_8B_MODEL_PATH = @"https://huggingface.co/lm-kit/phi-3.1-mini-4k-3.8b-instruct-gguf/resolve/main/Phi-3.1-mini-4k-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_QWEN2_7B_MODEL_PATH = @"https://huggingface.co/lm-kit/qwen-2-7b-instruct-gguf/resolve/main/Qwen-2-7B-Instruct-Q4_K_M.gguf";
        static readonly string DEFAULT_MISTRAL_NEMO_12_2B_MODEL_PATH = @"https://huggingface.co/lm-kit/mistral-nemo-2407-12.2b-instruct-gguf/resolve/main/Mistral-Nemo-2407-12.2B-Instruct-Q4_K_M.gguf";
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
            Console.WriteLine("0 - Mistral Nemo 2407 12.2B (requires approximately 7.7 GB of VRAM)");
            Console.WriteLine("1 - Meta Llama 3.1 8B (requires approximately 6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma2 9B Medium (requires approximately 7 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-3 3.82B Mini (requires approximately 3.3 GB of VRAM)");
            Console.WriteLine("4 - Alibaba Qwen-2 7.6B (requires approximately 5.6 GB of VRAM)");
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
                    modelLink = DEFAULT_PHI3_MINI_3_8B_MODEL_PATH;
                    break;
                case "4":
                    modelLink = DEFAULT_QWEN2_7B_MODEL_PATH;
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

            MultiTurnConversation chat = new MultiTurnConversation(model, contextSize: 2048)
            {
                MaximumCompletionTokens = 20,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = @"You are a fact-checking chatbot. Respond only with yes or no."
            };

            chat.Grammar = new Grammar(Grammar.PredefinedGrammar.Boolean);

            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            string prompt = "Hello chatbot, are you functioning properly?";

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();

                if (prompt == "")
                {
                    prompt = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(prompt))
                    {
                        break;
                    }
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
                TextGenerationResult result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(1)).Token);
               
                prompt = "";

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");
             }

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Prompts --");
            Console.WriteLine("Use '/reset' to start a fresh session.");
        }
        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}