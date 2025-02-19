using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using multi_turn_chat_with_agent_memory;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace multi_turn_chat_with_memory
{
    internal class Program
    {
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

        /*
          How many employess our customers usually have?
          In which industries are they working?
        */

        static async Task Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            //Loading model
            Console.WriteLine("Loading Alibaba Qwen 2.5 Instruct 0.5B model...");
            LM model = LM.LoadFromModelID("qwen2.5:0.5b",
                                          downloadingProgress: ModelDownloadingProgress,
                                          loadingProgress: ModelLoadingProgress);

            Console.WriteLine("\n\nLoading memory...");
            var memory = await MemoryBuilder.Generate();

            Console.Clear();

            MultiTurnConversation chat = new MultiTurnConversation(model, contextSize: 4096)
            {
                MaximumCompletionTokens = 1000,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You are BeeBop, our agent dedicated to providing information about the ideal customer profile of ACMEE Company. Provide clear and concise answers and include only factual content."
            };

            MultiTurnConversation chatMemory = new MultiTurnConversation(model, contextSize: 4096)
            {
                MaximumCompletionTokens = 1000,
                SamplingMode = new GreedyDecoding(),
                SystemPrompt = "You are BeeBop, our agent dedicated to providing information about the ideal customer profile of ACMEE Company. Provide clear and concise answers and include only factual content.",
                Memory = memory
            };

            chatMemory.MemoryRecall += (sender, e) =>
            {
                Debug.WriteLine("Memory recall event triggered with content: " + e.MemoryText);
            };


            chat.AfterTextCompletion += Chat_AfterTextCompletion;
            chatMemory.AfterTextCompletion += Chat_AfterTextCompletion;

            string prompt = "Hello, who are you?";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nAssistant: ");
                Console.ResetColor();
                TextGenerationResult result;

                result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");


                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("\n\nAssistant with Memory: ");
                Console.ResetColor();

                result = chatMemory.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.Write($"\n(gen. tokens: {result.GeneratedTokens.Count} - stop reason: {result.TerminationReason} - quality score: {Math.Round(result.QualityScore, 2)} - speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s - ctx usage: {result.ContextTokens.Count}/{result.ContextSize})");

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\n\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine();

            }

            Console.WriteLine("The chat ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }

        private static void Chat_AfterTextCompletion(object sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.Write(e.Text);
        }
    }
}