using LMKit.Data;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace multi_turn_chat_with_vision
{
    /// <summary>
    /// Demonstrates multi-turn chat with vision capabilities using LM-Kit SDK.
    /// Supports various vision-language models for image analysis and conversation.
    /// </summary>
    internal class Program
    {
        private static bool _isDownloading;

        /// <summary>
        /// Callback for model download progress reporting.
        /// </summary>
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

        /// <summary>
        /// Callback for model loading progress reporting.
        /// </summary>
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
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            // Display model selection menu
            Console.Clear();
            PrintHeader("Multi-Turn Chat with Vision Demo");
            Console.WriteLine("Select a vision-language model:\n");
            Console.WriteLine("  0 - MiniCPM o 4.5 9B           (~5.9 GB VRAM)");
            Console.WriteLine("  1 - Alibaba Qwen 3 VL 2B       (~2.5 GB VRAM)");
            Console.WriteLine("  2 - Alibaba Qwen 3 VL 4B       (~4 GB VRAM)");
            Console.WriteLine("  3 - Alibaba Qwen 3 VL 8B       (~6.5 GB VRAM)");
            Console.WriteLine("  4 - Google Gemma 3 4B          (~5.7 GB VRAM)");
            Console.WriteLine("  5 - Google Gemma 3 12B         (~11 GB VRAM)");
            Console.WriteLine("  6 - Mistral Ministral 3 3B     (~3.5 GB VRAM)");
            Console.WriteLine("  7 - Mistral Ministral 3 8B     (~6.5 GB VRAM)");
            Console.WriteLine("  8 - Mistral Ministral 3 14B    (~12 GB VRAM)");
            Console.WriteLine("  9 - Mistral Devstral Small 2   (~16 GB VRAM)");
            Console.WriteLine("\n  Or enter a custom model URI\n");
            Console.Write("> ");

            string input = Console.ReadLine() ?? string.Empty;
            string modelLink = GetModelLink(input.Trim());

            // Load the selected model
            Console.WriteLine();
            Uri modelUri = new(modelLink);
            LM model = new(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            // Initialize chat session
            Console.Clear();
            PrintHeader("Chat Session");
            PrintCommands();

            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.1f
                },
                SystemPrompt = "You are a chatbot that always responds promptly and helpfully to user requests."
            };

            chat.AfterTextCompletion += OnAfterTextCompletion;

            // Main chat loop
            string mode = "start_new_chat";
            string prompt = string.Empty;

            while (true)
            {
                Attachment? attachment = null;

                // Handle new chat initialization with image attachment
                if (mode == "start_new_chat")
                {
                    attachment = PromptForImageAttachment();

                    if (attachment == null)
                    {
                        break;
                    }

                    prompt = "describe the image";
                }

                // Generate response
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                TextGenerationResult result = mode switch
                {
                    "regenerate" => chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token),
                    "continue" => chat.ContinueLastAssistantResponse(new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token),
                    _ => chat.Submit(
                        attachment != null ? new ChatHistory.Message(prompt, attachment) : new ChatHistory.Message(prompt),
                        new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token)
                };

                mode = "chat";

                // Display generation statistics
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"[tokens: {result.GeneratedTokens.Count} | stop: {result.TerminationReason} | " +
                                  $"quality: {Math.Round(result.QualityScore, 2)} | speed: {Math.Round(result.TokenGenerationRate, 2)} tok/s | " +
                                  $"ctx: {result.ContextTokens.Count}/{result.ContextSize}]");
                Console.ResetColor();

                // Get user input
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nUser: ");
                Console.ResetColor();

                prompt = Console.ReadLine() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    break;
                }

                // Handle special commands
                switch (prompt.ToLowerInvariant())
                {
                    case "/reset":
                        chat.ClearHistory();
                        mode = "start_new_chat";
                        Console.Clear();
                        PrintHeader("Chat Session");
                        PrintCommands();
                        break;
                    case "/regenerate":
                        mode = "regenerate";
                        break;
                    case "/continue":
                        mode = "continue";
                        break;
                }
            }

            Console.WriteLine("\nChat session ended. Press any key to exit.");
            Console.ReadKey();
        }

        /// <summary>
        /// Returns the model URI based on user selection.
        /// </summary>
        private static string GetModelLink(string input)
        {
            string? modelId = input switch
            {
                "0" => "minicpm-o-45",
                "1" => "qwen3-vl:2b",
                "2" => "qwen3-vl:4b",
                "3" => "qwen3-vl:8b",
                "4" => "gemma3:4b",
                "5" => "gemma3:12b",
                "6" => "ministral3:3b",
                "7" => "ministral3:8b",
                "8" => "ministral3:14b",
                "9" => "devstral-small2",
                _ => null
            };

            if (modelId != null)
            {
                ModelCard? card = ModelCard.GetPredefinedModelCardByModelID(modelId);

                if (card != null)
                {
                    return card.ModelUri.ToString();
                }
            }

            return input.Trim('"');
        }

        /// <summary>
        /// Prompts the user to enter an image file path and creates an attachment.
        /// </summary>
        private static Attachment? PromptForImageAttachment()
        {
            while (true)
            {
                Console.Write("Enter the path to an image (or press Enter to exit):\n> ");
                string? path = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                try
                {
                    Attachment attachment = new(path.Trim('"'));
                    Console.WriteLine();
                    return attachment;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}\n");
                    Console.ResetColor();
                }
            }
        }

        /// <summary>
        /// Prints a formatted header.
        /// </summary>
        private static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"=== {title} ===\n");
            Console.ResetColor();
        }

        /// <summary>
        /// Prints available chat commands.
        /// </summary>
        private static void PrintCommands()
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Commands: /reset (new session) | /continue | /regenerate\n");
            Console.ResetColor();
        }

        /// <summary>
        /// Handles text completion events to display generated text with appropriate coloring.
        /// </summary>
        private static void OnAfterTextCompletion(object? sender, LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                TextSegmentType.ToolInvocation => ConsoleColor.Red,
                _ => ConsoleColor.White
            };

            Console.Write(e.Text);
            Console.ResetColor();
        }
    }
}