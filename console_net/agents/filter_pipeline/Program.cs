using LMKit.Agents;
using LMKit.Agents.Tools;
using LMKit.Agents.Tools.BuiltIn;
using LMKit.Model;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Events;
using LMKit.TextGeneration.Filters;
using LMKit.TextGeneration.Sampling;
using System.Diagnostics;
using System.Text;

namespace filter_pipeline
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
            Console.WriteLine("=== Filter / Middleware Pipeline Demo ===\n");
            Console.WriteLine("This demo shows how to use LM-Kit.NET's FilterPipeline to intercept");
            Console.WriteLine("and transform prompts, completions, and tool invocations.\n");
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

            // ──────────────────────────────────────────────────
            // Part 1: Prompt + Completion filters with MultiTurnConversation
            // ──────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Part 1: Prompt & Completion Filters                ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            RunPromptAndCompletionFiltersDemo(model);

            Console.WriteLine("\nPress any key to continue to Part 2...");
            Console.ReadKey(true);
            Console.Clear();

            // ──────────────────────────────────────────────────
            // Part 2: Tool invocation filters with Agent
            // ──────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Part 2: Tool Invocation Filters with Agent         ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            RunToolInvocationFiltersDemo(model);

            Console.WriteLine("\nPress any key to continue to Part 3...");
            Console.ReadKey(true);
            Console.Clear();

            // ──────────────────────────────────────────────────
            // Part 3: Interactive chat with all filters active
            // ──────────────────────────────────────────────────
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════════╗");
            Console.WriteLine("║  Part 3: Interactive Chat with All Filters Active   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine();

            RunInteractiveChat(model);

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey(true);
        }

        // ─────────────────────────────────────────────────────────────
        // Part 1: Prompt rewriting, logging, and completion quality gate
        // ─────────────────────────────────────────────────────────────
        static void RunPromptAndCompletionFiltersDemo(LM model)
        {
            Console.WriteLine("Demonstrating prompt filters (logging, rewriting) and completion");
            Console.WriteLine("filters (telemetry, quality gate).\n");

            var chat = new MultiTurnConversation(model)
            {
                SystemPrompt = "You are a helpful assistant. Be concise.",
                MaximumCompletionTokens = 512,
                SamplingMode = new RandomSampling { Temperature = 0.8f }
            };

            var stopwatch = new Stopwatch();

            // Build filter pipeline using lambda-friendly API
            chat.Filters = new FilterPipeline()

                // Filter 1: Logging prompt filter (logs what goes in)
                .AddPromptFilter(async (ctx, next) =>
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  [PROMPT FILTER: Logger]");
                    Console.WriteLine($"    Input prompt: \"{Truncate(ctx.Prompt, 80)}\"");
                    Console.ResetColor();

                    stopwatch.Restart();
                    await next(ctx);
                    stopwatch.Stop();

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"    Inference completed in {stopwatch.ElapsedMilliseconds}ms");
                    Console.ResetColor();
                })

                // Filter 2: Prompt rewriter (appends a constraint)
                .AddPromptFilter(async (ctx, next) =>
                {
                    string original = ctx.Prompt;
                    ctx.Prompt = original + "\n\n(Please respond in three sentences or fewer.)";

                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine($"  [PROMPT FILTER: Rewriter]");
                    Console.WriteLine($"    Added brevity constraint to prompt");
                    Console.ResetColor();

                    await next(ctx);
                })

                // Filter 3: Completion telemetry
                .AddCompletionFilter(async (ctx, next) =>
                {
                    await next(ctx);

                    if (ctx.Result != null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"\n  [COMPLETION FILTER: Telemetry]");
                        Console.WriteLine($"    Tokens generated: {ctx.Result.GeneratedTokens.Count}");
                        Console.WriteLine($"    Speed: {ctx.Result.TokenGenerationRate:F1} tok/s");
                        Console.WriteLine($"    Quality: {ctx.Result.QualityScore:F2}");
                        Console.ResetColor();
                    }
                });

            chat.AfterTextCompletion += (_, e) =>
            {
                if (e.SegmentType == TextSegmentType.UserVisible)
                    Console.Write(e.Text);
            };

            // Run a sample prompt
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nUser: ");
            Console.ResetColor();
            Console.WriteLine("What are the benefits of middleware patterns in software architecture?");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nAssistant: ");
            Console.ResetColor();

            chat.Submit(
                "What are the benefits of middleware patterns in software architecture?",
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

            Console.WriteLine();
        }

        // ─────────────────────────────────────────────────────────────
        // Part 2: Tool invocation filters with Agent
        // ─────────────────────────────────────────────────────────────
        static void RunToolInvocationFiltersDemo(LM model)
        {
            Console.WriteLine("Demonstrating tool invocation filters: logging, rate limiting,");
            Console.WriteLine("and result caching.\n");

            int toolCallCount = 0;
            const int MaxToolCalls = 5;
            var toolCache = new Dictionary<string, ToolCallResult>();

            var filters = new FilterPipeline()

                // Filter 1: Tool call logger
                .AddToolInvocationFilter(async (ctx, next) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"\n  [TOOL FILTER: Logger]");
                    Console.WriteLine($"    Tool: {ctx.ToolCall.Name}");
                    Console.WriteLine($"    Args: {Truncate(ctx.ToolCall.ArgumentsJson, 100)}");
                    Console.WriteLine($"    Batch position: {ctx.ToolIndex + 1}/{ctx.ToolCount}");
                    Console.ResetColor();

                    await next(ctx);

                    if (ctx.Result != null)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"    Result: {Truncate(ctx.Result.ResultJson ?? "", 120)}");
                        Console.ResetColor();
                    }
                })

                // Filter 2: Rate limiter
                .AddToolInvocationFilter(async (ctx, next) =>
                {
                    toolCallCount++;

                    if (toolCallCount > MaxToolCalls)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"  [TOOL FILTER: Rate Limiter] Blocked call #{toolCallCount} (max {MaxToolCalls})");
                        Console.ResetColor();
                        ctx.Cancel = true;
                        return;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkCyan;
                    Console.WriteLine($"  [TOOL FILTER: Rate Limiter] Call {toolCallCount}/{MaxToolCalls} allowed");
                    Console.ResetColor();

                    await next(ctx);
                })

                // Filter 3: Simple result cache
                .AddToolInvocationFilter(async (ctx, next) =>
                {
                    string cacheKey = $"{ctx.ToolCall.Name}:{ctx.ToolCall.ArgumentsJson}";

                    if (toolCache.TryGetValue(cacheKey, out var cached))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [TOOL FILTER: Cache] HIT for {ctx.ToolCall.Name}");
                        Console.ResetColor();
                        ctx.Result = cached;
                        return; // skip actual invocation
                    }

                    await next(ctx);

                    if (ctx.Result != null)
                    {
                        toolCache[cacheKey] = ctx.Result;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  [TOOL FILTER: Cache] Stored result for {ctx.ToolCall.Name}");
                        Console.ResetColor();
                    }
                });

            // Build the agent with filters
            var agent = Agent.CreateBuilder(model)
                .WithInstruction("You are a helpful assistant with access to a calculator and date/time tools. Use them when needed.")
                .WithTools(tools =>
                {
                    tools.Register(BuiltInTools.CalcArithmetic);
                    tools.Register(BuiltInTools.DateTimeNow);
                })
                .WithFilters(filters)
                .WithMaxIterations(5)
                .Build();

            using var executor = new AgentExecutor();

            executor.AfterTextCompletion += (_, e) =>
            {
                if (e.SegmentType == TextSegmentType.UserVisible)
                    Console.Write(e.Text);
            };

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("User: ");
            Console.ResetColor();
            Console.WriteLine("What is 42 * 17? Also, what is today's date and what day of the week is it?");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nAssistant: ");
            Console.ResetColor();

            var result = executor.ExecuteAsync(
                agent,
                "What is 42 * 17? Also, what is today's date and what day of the week is it?",
                new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token).GetAwaiter().GetResult();

            Console.WriteLine($"\n\n  Tool calls made: {toolCallCount}");
            Console.WriteLine($"  Cache entries: {toolCache.Count}");
        }

        // ─────────────────────────────────────────────────────────────
        // Part 3: Interactive multi-turn chat with full filter pipeline
        // ─────────────────────────────────────────────────────────────
        static void RunInteractiveChat(LM model)
        {
            Console.WriteLine("Interactive chat with all three filter types active.");
            Console.WriteLine("Type your messages, or use '/reset' to clear history.\n");
            ShowSpecialPrompts();

            var requestLog = new List<string>();
            int turnCount = 0;

            var chat = new MultiTurnConversation(model)
            {
                SystemPrompt = "You are a helpful assistant with access to a calculator and date/time tools.",
                MaximumCompletionTokens = 2048,
                SamplingMode = new RandomSampling { Temperature = 0.8f }
            };

            chat.Tools.Register(BuiltInTools.CalcArithmetic);
            chat.Tools.Register(BuiltInTools.DateTimeNow);

            // Build a comprehensive filter pipeline
            chat.Filters = new FilterPipeline()

                // Prompt: turn counter and timing
                .AddPromptFilter(async (ctx, next) =>
                {
                    turnCount++;
                    ctx.Properties["turnNumber"] = turnCount;
                    ctx.Properties["startTime"] = Stopwatch.GetTimestamp();

                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  [Turn #{turnCount}] Processing prompt...");
                    Console.ResetColor();

                    await next(ctx);
                })

                // Completion: timing and log
                .AddCompletionFilter(async (ctx, next) =>
                {
                    await next(ctx);

                    if (ctx.Properties.TryGetValue("startTime", out var startObj)
                        && startObj is long startTs)
                    {
                        long elapsed = Stopwatch.GetTimestamp() - startTs;
                        double ms = (double)elapsed / Stopwatch.Frequency * 1000;

                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.Write($"\n  (turn #{ctx.Properties["turnNumber"]}, {ms:F0}ms");
                        if (ctx.Result != null)
                            Console.Write($", {ctx.Result.GeneratedTokens.Count} tokens, {ctx.Result.TokenGenerationRate:F1} tok/s");
                        Console.WriteLine(")");
                        Console.ResetColor();
                    }

                    if (ctx.Result != null)
                        requestLog.Add($"Turn {ctx.Properties["turnNumber"]}: {ctx.Result.GeneratedTokens.Count} tokens");
                })

                // Tool: log each invocation
                .AddToolInvocationFilter(async (ctx, next) =>
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write($"  [{ctx.ToolCall.Name}] ");
                    Console.ResetColor();

                    await next(ctx);

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"-> {Truncate(ctx.Result?.ResultJson ?? "(no result)", 80)}");
                    Console.ResetColor();
                });

            chat.AfterTextCompletion += (_, e) =>
            {
                Console.ForegroundColor = e.SegmentType switch
                {
                    TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                    TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                    _ => ConsoleColor.White
                };
                Console.Write(e.Text);
            };

            string prompt = "Greet the user briefly. Mention you have calculator and date/time tools.";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant: ");
                Console.ResetColor();

                var result = chat.Submit(
                    prompt,
                    new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($"\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

                if (string.Compare(prompt, "/reset", ignoreCase: true) == 0)
                {
                    chat.ClearHistory();
                    turnCount = 0;
                    requestLog.Clear();
                    Console.WriteLine("Session reset.\n");
                    prompt = "Greet the user briefly. Mention you have calculator and date/time tools.";
                }
                else if (string.Compare(prompt, "/stats", ignoreCase: true) == 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n  Session stats ({requestLog.Count} turns):");
                    foreach (var entry in requestLog)
                        Console.WriteLine($"    {entry}");
                    Console.ResetColor();
                    Console.Write("\nUser: ");
                    prompt = Console.ReadLine() ?? string.Empty;
                }
            }
        }

        private static void ShowSpecialPrompts()
        {
            Console.WriteLine("-- Special Commands --");
            Console.WriteLine("  /reset   - Start a fresh session");
            Console.WriteLine("  /stats   - Show session statistics from completion filter\n");
        }

        private static string Truncate(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("\r", "").Replace("\n", " ");
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
