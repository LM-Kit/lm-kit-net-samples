using LMKit.Model;
using LMKit.Telemetry;
using LMKit.TextGeneration;
using LMKit.TextGeneration.Sampling;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;

namespace telemetry_observability
{
    internal class Program
    {
        static bool _isDownloading;

        // In-memory telemetry storage
        static readonly ConcurrentBag<ActivityInfo> _activities = new();
        static readonly ConcurrentDictionary<string, List<double>> _histograms = new();
        static ActivityListener? _activityListener;
        static MeterListener? _meterListener;

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
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

        private static bool OnLoadProgress(float progress)
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

            // Configure in-memory telemetry collection
            ConfigureTelemetryListeners();

            Console.Clear();
            PrintHeader();

            Console.WriteLine("Please select the model you want to use:\n");
            Console.WriteLine("0 - Google Gemma 3 4B (requires approximately 4 GB of VRAM)");
            Console.WriteLine("1 - Alibaba Qwen-3 8B (requires approximately 5.6 GB of VRAM)");
            Console.WriteLine("2 - Google Gemma 3 12B (requires approximately 9 GB of VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B (requires approximately 11 GB of VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B (requires approximately 16 GB of VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B (requires approximately 18 GB of VRAM)");
            Console.WriteLine("6 - Alibaba Qwen 3.5 27B (requires approximately 18 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string? modelId = input?.Trim() switch
            {
                "0" => "gemma3:4b",
                "1" => "qwen3:8b",
                "2" => "gemma3:12b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            // Load model
            LM model = LoadModel(modelId, input);

            Console.Clear();
            PrintHeader();

            // Create conversation with telemetry-enabled chat history
            MultiTurnConversation chat = new(model)
            {
                MaximumCompletionTokens = 512,
                SamplingMode = new RandomSampling()
                {
                    Temperature = 0.7f
                },
                SystemPrompt = "You are a helpful assistant. Keep responses concise."
            };

            // Display conversation ID for correlation
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Conversation ID: {chat.ChatHistory.ConversationId}");
            Console.WriteLine("(This ID correlates all telemetry spans for this session)\n");
            Console.ResetColor();

            ShowCommands();

            chat.AfterTextCompletion += Chat_AfterTextCompletion;

            string prompt = "Hello! Please introduce yourself briefly.";

            while (!string.IsNullOrWhiteSpace(prompt))
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nAssistant: ");
                Console.ResetColor();

                // Each Submit call generates telemetry data
                TextGenerationResult result = chat.Submit(
                    prompt,
                    new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);

                // Display inference statistics
                Console.WriteLine();
                PrintInferenceStats(result);

                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("\nUser: ");
                Console.ResetColor();
                prompt = Console.ReadLine() ?? string.Empty;

                if (string.Equals(prompt, "/traces", StringComparison.OrdinalIgnoreCase))
                {
                    PrintTraces();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nUser: ");
                    Console.ResetColor();
                    prompt = Console.ReadLine() ?? string.Empty;
                }
                else if (string.Equals(prompt, "/metrics", StringComparison.OrdinalIgnoreCase))
                {
                    PrintMetrics();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nUser: ");
                    Console.ResetColor();
                    prompt = Console.ReadLine() ?? string.Empty;
                }
                else if (string.Equals(prompt, "/info", StringComparison.OrdinalIgnoreCase))
                {
                    PrintTelemetryInfo();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nUser: ");
                    Console.ResetColor();
                    prompt = Console.ReadLine() ?? string.Empty;
                }
                else if (string.Equals(prompt, "/clear", StringComparison.OrdinalIgnoreCase))
                {
                    _activities.Clear();
                    _histograms.Clear();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Telemetry data cleared.");
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("\nUser: ");
                    Console.ResetColor();
                    prompt = Console.ReadLine() ?? string.Empty;
                }
                else if (string.Equals(prompt, "/reset", StringComparison.OrdinalIgnoreCase))
                {
                    chat.ClearHistory();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Chat history cleared. Starting fresh conversation.");
                    Console.WriteLine($"New Conversation ID: {chat.ChatHistory.ConversationId}");
                    Console.ResetColor();
                    prompt = "Hello!";
                }
            }

            // Cleanup
            _activityListener?.Dispose();
            _meterListener?.Dispose();

            Console.WriteLine("Demo ended. Press any key to exit.");
            Console.ReadKey();
        }

        private static LM LoadModel(string? modelId, string? rawInput)
        {
            if (modelId != null)
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            // Custom URI fallback
            string uri = !string.IsNullOrWhiteSpace(rawInput) ? rawInput.Trim().Trim('"') : "gemma3:4b";

            // If it looks like a model ID (no slashes, no dots besides the colon separator), treat it as one
            if (!uri.Contains('/') && !uri.Contains('.'))
            {
                return LM.LoadFromModelID(
                    uri,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(uri),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static void ConfigureTelemetryListeners()
        {
            // Listen to LM-Kit activities (traces)
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == LMKitTelemetry.ActivitySourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity =>
                {
                    var info = new ActivityInfo
                    {
                        OperationName = activity.OperationName,
                        DisplayName = activity.DisplayName,
                        Duration = activity.Duration,
                        StartTime = activity.StartTimeUtc,
                        Status = activity.Status,
                        Tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value?.ToString() ?? "")
                    };
                    _activities.Add(info);
                }
            };
            ActivitySource.AddActivityListener(_activityListener);

            // Listen to LM-Kit metrics
            _meterListener = new MeterListener();
            _meterListener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == LMKitTelemetry.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
            {
                string key = instrument.Name;
                if (!_histograms.ContainsKey(key))
                {
                    _histograms[key] = new List<double>();
                }
                _histograms[key].Add(value);
            });

            _meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, state) =>
            {
                string key = instrument.Name;
                // Include token type in key for token usage metric
                foreach (var tag in tags)
                {
                    if (tag.Key == "gen_ai.token.type")
                    {
                        key = $"{instrument.Name} ({tag.Value})";
                        break;
                    }
                }
                if (!_histograms.ContainsKey(key))
                {
                    _histograms[key] = new List<double>();
                }
                _histograms[key].Add(value);
            });

            _meterListener.Start();

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Telemetry listeners configured!");
            Console.WriteLine($"  Activity Source: {LMKitTelemetry.ActivitySourceName}");
            Console.WriteLine($"  Meter: {LMKitTelemetry.MeterName}");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("==============================================");
            Console.WriteLine("   LM-Kit.NET Telemetry & Observability Demo  ");
            Console.WriteLine("==============================================");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void ShowCommands()
        {
            Console.WriteLine("Commands:");
            Console.WriteLine("  /traces  - Display collected trace spans");
            Console.WriteLine("  /metrics - Display collected metrics");
            Console.WriteLine("  /info    - Show telemetry configuration info");
            Console.WriteLine("  /clear   - Clear collected telemetry data");
            Console.WriteLine("  /reset   - Clear chat history and start fresh");
            Console.WriteLine("  (empty)  - Exit the demo\n");
        }

        private static void PrintInferenceStats(TextGenerationResult result)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine($"  Tokens: {result.GeneratedTokens.Count} generated, {result.PromptTokenCount} prompt");
            Console.WriteLine($"  Speed: {result.TokenGenerationRate:F1} tok/s");
            Console.WriteLine($"  Stop reason: {result.TerminationReason}");
            Console.WriteLine($"  Context: {result.ContextTokens.Count}/{result.ContextSize}");
            Console.ResetColor();
        }

        private static void PrintTraces()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Collected Trace Spans ---");

            if (_activities.IsEmpty)
            {
                Console.WriteLine("No spans collected yet.");
            }
            else
            {
                int count = 0;
                foreach (var activity in _activities.Reverse().Take(10))
                {
                    count++;
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"\n[{count}] {activity.DisplayName}");
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"    Duration: {activity.Duration.TotalMilliseconds:F2}ms");
                    Console.WriteLine($"    Status: {activity.Status}");

                    // Show key GenAI attributes
                    foreach (var tag in activity.Tags)
                    {
                        if (tag.Key.StartsWith("gen_ai.") || tag.Key == "error.type")
                        {
                            Console.WriteLine($"    {tag.Key}: {tag.Value}");
                        }
                    }
                }

                if (_activities.Count > 10)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"\n... and {_activities.Count - 10} more spans");
                }
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        private static void PrintMetrics()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Collected Metrics ---");

            if (_histograms.IsEmpty)
            {
                Console.WriteLine("No metrics collected yet.");
            }
            else
            {
                foreach (var kvp in _histograms.OrderBy(k => k.Key))
                {
                    var values = kvp.Value;
                    if (values.Count > 0)
                    {
                        double avg = values.Average();
                        double min = values.Min();
                        double max = values.Max();
                        double sum = values.Sum();

                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"\n{kvp.Key}:");
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"    Count: {values.Count}");
                        Console.WriteLine($"    Sum: {sum:F2}");
                        Console.WriteLine($"    Avg: {avg:F4}");
                        Console.WriteLine($"    Min: {min:F4}");
                        Console.WriteLine($"    Max: {max:F4}");
                    }
                }
            }

            Console.WriteLine();
            Console.ResetColor();
        }

        private static void PrintTelemetryInfo()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n--- Telemetry Configuration ---");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine($"Provider: {LMKitTelemetry.ProviderName}");
            Console.WriteLine($"Version: {LMKitTelemetry.InstrumentationVersion}");
            Console.WriteLine($"Activity Source: {LMKitTelemetry.ActivitySourceName}");
            Console.WriteLine($"Meter: {LMKitTelemetry.MeterName}");
            Console.WriteLine();
            Console.WriteLine("Operations tracked:");
            Console.WriteLine($"  - {LMKitTelemetry.OperationTextCompletion}");
            Console.WriteLine($"  - {LMKitTelemetry.OperationEmbeddings}");
            Console.WriteLine($"  - {LMKitTelemetry.OperationInvokeAgent}");
            Console.WriteLine($"  - {LMKitTelemetry.OperationExecuteTool}");
            Console.WriteLine();
            Console.WriteLine("Metrics available:");
            Console.WriteLine("  - gen_ai.server.time_to_first_token (seconds)");
            Console.WriteLine("  - gen_ai.server.time_per_output_token (seconds)");
            Console.WriteLine("  - gen_ai.server.request.duration (seconds)");
            Console.WriteLine("  - gen_ai.client.token.usage (tokens)");
            Console.WriteLine("  - gen_ai.client.operation.duration (seconds)");
            Console.WriteLine();
            Console.WriteLine("Span attributes:");
            Console.WriteLine("  - gen_ai.conversation.id");
            Console.WriteLine("  - gen_ai.response.id");
            Console.WriteLine("  - gen_ai.response.finish_reasons");
            Console.WriteLine("  - gen_ai.request.temperature, top_p, top_k, max_tokens");
            Console.WriteLine("  - gen_ai.usage.input_tokens, output_tokens");
            Console.WriteLine();
            Console.ResetColor();
        }

        private static void Chat_AfterTextCompletion(
            object? sender,
            LMKit.TextGeneration.Events.AfterTextCompletionEventArgs e)
        {
            Console.ForegroundColor = e.SegmentType switch
            {
                LMKit.TextGeneration.Chat.TextSegmentType.InternalReasoning => ConsoleColor.Blue,
                LMKit.TextGeneration.Chat.TextSegmentType.ToolInvocation => ConsoleColor.Magenta,
                _ => ConsoleColor.White
            };

            Console.Write(e.Text);
        }
    }

    internal class ActivityInfo
    {
        public string OperationName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public TimeSpan Duration { get; set; }
        public DateTime StartTime { get; set; }
        public ActivityStatusCode Status { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }
}
