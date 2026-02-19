using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Sampling;
using multi_turn_chat_with_agent_memory;
using System.Diagnostics;
using System.Text;

namespace multi_turn_chat_with_memory
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

        static async Task Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Loading Alibaba Qwen 3 Instruct 0.6B model...");
            LM model = LM.LoadFromModelID(
                "qwen3:0.6b",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);

            Console.WriteLine("\n\nLoading memory...");
            var memory = await MemoryBuilder.Generate();
            Console.WriteLine($"Memory loaded: {memory.EntryCount} entries.");

            Console.Clear();

            MultiTurnConversation chat = new(model, contextSize: 4096)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You are BeeBop, our agent dedicated to providing information about the ideal customer profile of ACMEE Company. Provide clear and concise answers and include only factual content."
            };

            MultiTurnConversation chatMemory = new(model, contextSize: 4096)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You are BeeBop, our agent dedicated to providing information about the ideal customer profile of ACMEE Company. Provide clear and concise answers and include only factual content.",
                Memory = memory
            };

            chatMemory.MemoryRecall += (sender, e) =>
            {
                Debug.WriteLine("Memory recall event triggered with content: " + e.MemoryText);
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;
            chatMemory.AfterTextCompletion += OnAfterTextCompletion;

            string prompt = "Hello, who are you?";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nAssistant: ");
                Console.ResetColor();

                var result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n\nAssistant with Memory: ");
                Console.ResetColor();

                result = chatMemory.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
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
