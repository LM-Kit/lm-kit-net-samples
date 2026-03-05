using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using System.Text;

namespace multi_turn_chat_with_code_writing_assistant
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
                "0" => "qwen3.5:9b",
                "1" => "gptoss:20b",
                "2" => "devstral-small2",
                "3" => "qwen3-coder:30b-a3b",
                "4" => "glm4.7-flash",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3.5:9b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Code Writing Assistant Demo ===\n");
            Console.WriteLine("A multi-turn assistant that can read, write, and modify source files.\n");

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Alibaba Qwen 3.5 9B           (~7 GB VRAM)");
            Console.WriteLine("1 - OpenAI GPT OSS 20B            (~16 GB VRAM)");
            Console.WriteLine("2 - Mistral Devstral Small 2 24B  (~16 GB VRAM)");
            Console.WriteLine("3 - Alibaba Qwen 3 Coder 30B-A3B  (~18 GB VRAM) [Recommended]");
            Console.WriteLine("4 - Z.ai GLM 4.7 Flash 30B        (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? input = Console.ReadLine();
            LM model = LoadModel(input ?? "");

            Console.Clear();

            // Set up multi-turn conversation with a code-writing system prompt
            var chat = new MultiTurnConversation(model)
            {
                MaximumCompletionTokens = 4096,
                SystemPrompt =
                    "You are a code writing assistant. You can read, create, and modify source files, " +
                    "list directories, search for files, and look things up on the web.\n\n" +
                    "When asked to write or modify code:\n" +
                    "1. Read the existing file first (if it exists) to understand the current state.\n" +
                    "2. Write the complete updated file using your file write tool.\n" +
                    "3. Briefly explain what you changed and why.\n\n" +
                    "When creating new files, confirm the file path with the user before writing. " +
                    "Be concise and show relevant code snippets in your explanations."
            };

            // Register read and navigation tools
            chat.Tools.Register(BuiltInTools.FileSystemRead);
            chat.Tools.Register(BuiltInTools.FileSystemList);
            chat.Tools.Register(BuiltInTools.FileSystemSearch);

            // Register the write tool so the model can create and modify files
            chat.Tools.Register(BuiltInTools.FileSystemWrite);

            // Register web search for documentation lookups
            chat.Tools.Register(BuiltInTools.WebSearch);

            chat.AfterTextCompletion += OnAfterTextCompletion;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Code Writing Assistant");
            Console.ResetColor();
            Console.WriteLine("A multi-turn chat that can read, create, and modify files on disk.");
            Console.WriteLine("Ask it to write new code, refactor existing files, or scaffold entire projects.\n");
            Console.WriteLine("Tools: file read, file write, directory listing, file search, web search");
            Console.WriteLine("Commands: /reset (clear history) | /regenerate (redo last response) | q (quit)\n");

            string prompt = "Hello!";

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                try
                {
                    TextGenerationResult result;

                    if (string.Equals(prompt, "/regenerate", StringComparison.OrdinalIgnoreCase))
                    {
                        result = chat.RegenerateResponse(new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                    }
                    else
                    {
                        result = chat.Submit(prompt, new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                    }

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"\n({result.GeneratedTokens.Count} tokens");
                    Console.Write($" | {Math.Round(result.TokenGenerationRate, 1)} tok/s");
                    Console.Write($" | ctx {result.ContextTokens.Count}/{result.ContextSize})");
                    Console.ResetColor();
                }
                catch (LMKit.Exceptions.NotEnoughContextSizeException e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Context full ({e.RequiredTokens} needed, {e.ContextSize} available). Use /reset to start fresh.");
                    Console.ResetColor();
                }
                catch (OperationCanceledException)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Timed out.");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {ex.Message}");
                    Console.ResetColor();
                }

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"\n\nYou: ");
                Console.ResetColor();
                prompt = Console.ReadLine()?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(prompt))
                    continue;

                if (string.Equals(prompt, "q", StringComparison.OrdinalIgnoreCase))
                    break;

                if (string.Equals(prompt, "/reset", StringComparison.OrdinalIgnoreCase))
                {
                    chat.ClearHistory();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("History cleared.\n");
                    Console.ResetColor();
                    prompt = "Hello!";
                }
            }

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        static void OnAfterTextCompletion(object? sender, AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                _ => ConsoleColor.White
            };
            Console.Write(e.Text);
            Console.ResetColor();
        }
    }
}
