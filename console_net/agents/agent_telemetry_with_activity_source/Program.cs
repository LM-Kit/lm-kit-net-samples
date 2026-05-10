using LMKit.Agents;
using LMKit.Agents.Observability;
using LMKit.Agents.Orchestration;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Diagnostics;
using System.Text;

namespace agent_telemetry_with_activity_source
{
    /// <summary>
    /// Demonstrates the OpenTelemetry-compatible distributed-trace surface added in
    /// LM-Kit.NET 2026.5.2 via <see cref="AgentDiagnostics"/>.
    ///
    /// The agent runtime emits standard <see cref="ActivitySource"/> spans for every
    /// orchestration run, every per-agent invocation, and every supervisor delegation.
    /// Subscribers register an <see cref="ActivityListener"/> (this demo) or wire the
    /// source into OpenTelemetry's <c>TracerProviderBuilder.AddSource</c> for production
    /// export to Jaeger / Tempo / Datadog / Application Insights / etc.
    /// </summary>
    internal static class Program
    {
        private const string LicenseKey = "";

        private const ConsoleColor ColorBanner       = ConsoleColor.White;
        private const ConsoleColor ColorMuted        = ConsoleColor.DarkGray;
        private const ConsoleColor ColorOrchestration= ConsoleColor.Cyan;
        private const ConsoleColor ColorAgent        = ConsoleColor.Green;
        private const ConsoleColor ColorDelegate     = ConsoleColor.Yellow;
        private const ConsoleColor ColorError        = ConsoleColor.Red;

        private static bool _isDownloading;
        private static readonly List<TraceEntry> _trace = new();

        private static async Task Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(LicenseKey);
            Console.OutputEncoding = Encoding.UTF8;
            try { Console.InputEncoding = Encoding.UTF8; } catch { /* not interactive */ }
            TryClear();

            PrintBanner();
            PrintIntro();

            using var model = SelectModel();

            // Subscribe the listener BEFORE any orchestration runs.
            // In production, swap this for:
            //     Sdk.CreateTracerProviderBuilder()
            //        .AddSource(AgentDiagnostics.SourceName)
            //        .AddOtlpExporter()
            //        .Build();
            using var listener = BuildConsoleActivityListener();
            ActivitySource.AddActivityListener(listener);

            TryClear();
            PrintBanner();
            WriteColored(
                $"Listening on ActivitySource '{AgentDiagnostics.SourceName}'. " +
                $"Spans emitted by every orchestrator/agent will appear below.",
                ColorMuted);
            Console.WriteLine();

            await RunPipelineScenarioAsync(model);
            await RunSupervisorScenarioAsync(model);

            PrintTraceTree();

            if (!Console.IsInputRedirected)
            {
                Console.WriteLine();
                WriteColored("Press Enter to exit.", ColorMuted);
                Console.ReadLine();
            }
        }

        // ----------------------------------------------------------------------
        // Scenario 1: Pipeline -> orchestration.execute + 2x agent.execute
        // ----------------------------------------------------------------------

        private static async Task RunPipelineScenarioAsync(LM model)
        {
            WriteColored(new string('-', 78), ColorBanner);
            WriteColored("Scenario 1: PipelineOrchestrator (research -> write)", ColorBanner);
            WriteColored("Expect: 1x orchestration.execute, 2x agent.execute", ColorMuted);
            WriteColored(new string('-', 78), ColorBanner);
            Console.WriteLine();

            var researcher = Agent.CreateBuilder(model)
                .WithPersona("Researcher")
                .WithInstruction("In one short paragraph, summarize what the user asks about.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var writer = Agent.CreateBuilder(model)
                .WithPersona("Writer")
                .WithInstruction("Rewrite the previous paragraph as a tight 2-sentence tagline.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var orchestrator = new PipelineOrchestrator()
                .AddStage("research", researcher)
                .AddStage("write", writer);

            var options = new OrchestrationOptions
            {
                SamplingMode = new GreedyDecoding(),
                MaxCompletionTokens = 96,
                ReasoningLevel = ReasoningLevel.None
            };

            var result = await orchestrator.ExecuteAsync(
                "Edge AI inference on consumer hardware",
                options);

            Console.WriteLine();
            WriteColored("Final tagline:", ColorBanner);
            WriteColored("  " + (result.Content ?? "<empty>").Trim(), ColorBanner);
            WriteColored(
                $"  ({result.AgentResults.Count} agents, {result.TotalInferenceCount} inferences, " +
                $"{result.Duration.TotalSeconds:F1}s)",
                ColorMuted);
            Console.WriteLine();
        }

        // ----------------------------------------------------------------------
        // Scenario 2: Supervisor -> orchestration.execute + agent.execute + agent.delegate
        // ----------------------------------------------------------------------

        private static async Task RunSupervisorScenarioAsync(LM model)
        {
            WriteColored(new string('-', 78), ColorBanner);
            WriteColored("Scenario 2: SupervisorOrchestrator (supervisor delegating to workers)", ColorBanner);
            WriteColored("Expect: 1x orchestration.execute, 1+x agent.execute, 1+x agent.delegate", ColorMuted);
            WriteColored(new string('-', 78), ColorBanner);
            Console.WriteLine();

            var supervisorAgent = Agent.CreateBuilder(model)
                .WithPersona("Supervisor")
                .WithInstruction(
                    "You coordinate two specialists: 'Coder' for implementation tasks and " +
                    "'Marketer' for messaging tasks. Use the delegate_to_agent tool to " +
                    "delegate when needed, then summarize their answers in one sentence.")
                .WithPlanning(PlanningStrategy.ReAct)
                .Build();

            var coder = Agent.CreateBuilder(model)
                .WithPersona("Coder")
                .WithInstruction("You write small code snippets. Reply in 2 sentences max.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var marketer = Agent.CreateBuilder(model)
                .WithPersona("Marketer")
                .WithInstruction("You write short marketing copy. Reply in 2 sentences max.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var supervisor = new SupervisorOrchestrator(supervisorAgent)
                .AddWorker(coder)
                .AddWorker(marketer);

            var options = new OrchestrationOptions
            {
                SamplingMode = new GreedyDecoding(),
                MaxCompletionTokens = 192,
                MaxSteps = 6,
                ReasoningLevel = ReasoningLevel.None
            };

            var result = await supervisor.ExecuteAsync(
                "Write a one-line Python snippet that reads JSON from stdin, then a " +
                "tagline for it.",
                options);

            Console.WriteLine();
            WriteColored("Supervisor result:", ColorBanner);
            WriteColored("  " + (result.Content ?? "<empty>").Trim(), ColorBanner);
            WriteColored(
                $"  ({result.AgentResults.Count} agents involved, {result.TotalInferenceCount} inferences, " +
                $"{result.Duration.TotalSeconds:F1}s)",
                ColorMuted);
            Console.WriteLine();
        }

        // ----------------------------------------------------------------------
        // ActivityListener: subscribed to AgentDiagnostics.SourceName ('LMKit.Agents')
        // ----------------------------------------------------------------------

        private static ActivityListener BuildConsoleActivityListener()
        {
            return new ActivityListener
            {
                ShouldListenTo = src => src.Name == AgentDiagnostics.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = OnSpanStopped
            };
        }

        private static void OnSpanStopped(Activity activity)
        {
            // Capture for the trace-tree summary at the end.
            _trace.Add(new TraceEntry(
                Operation: activity.OperationName,
                DurationMs: activity.Duration.TotalMilliseconds,
                Status: activity.Status,
                Tags: SnapshotTags(activity)));

            // Live print, color-coded by span kind.
            var color = activity.OperationName switch
            {
                "orchestration.execute" => ColorOrchestration,
                "agent.execute"         => ColorAgent,
                "agent.delegate"        => ColorDelegate,
                _                       => ColorMuted
            };
            if (activity.Status == ActivityStatusCode.Error) color = ColorError;

            string symbol = activity.OperationName switch
            {
                "orchestration.execute" => "[ORCH ]",
                "agent.execute"         => "[AGENT]",
                "agent.delegate"        => "[DELEG]",
                _                       => "[span ]"
            };

            WriteColored(
                $"  {symbol} {activity.OperationName,-22} " +
                $"{activity.Duration.TotalMilliseconds,7:F0} ms  status={activity.Status}",
                color);

            string tags = FormatInterestingTags(activity);
            if (!string.IsNullOrEmpty(tags))
            {
                WriteColored("           " + tags, ColorMuted);
            }
        }

        private static IReadOnlyDictionary<string, string> SnapshotTags(Activity a)
        {
            var dict = new Dictionary<string, string>();
            foreach (var kvp in a.TagObjects)
            {
                if (kvp.Value != null)
                {
                    dict[kvp.Key] = kvp.Value.ToString() ?? string.Empty;
                }
            }
            return dict;
        }

        private static string FormatInterestingTags(Activity a)
        {
            var interesting = new[]
            {
                AgentDiagnostics.TagAgentName,
                AgentDiagnostics.TagOrchestratorName,
                AgentDiagnostics.TagStep,
                AgentDiagnostics.TagPlanningStrategy,
                AgentDiagnostics.TagInferenceCount,
                AgentDiagnostics.TagDelegationFrom,
                AgentDiagnostics.TagDelegationTo
            };

            var pairs = new List<string>();
            foreach (var key in interesting)
            {
                var value = a.GetTagItem(key);
                if (value != null) pairs.Add($"{key}={value}");
            }

            return pairs.Count == 0 ? string.Empty : string.Join(", ", pairs);
        }

        // ----------------------------------------------------------------------
        // Trace summary
        // ----------------------------------------------------------------------

        private record TraceEntry(
            string Operation,
            double DurationMs,
            ActivityStatusCode Status,
            IReadOnlyDictionary<string, string> Tags);

        private static void PrintTraceTree()
        {
            Console.WriteLine();
            WriteColored(new string('=', 78), ColorBanner);
            WriteColored("  Span summary (what was emitted to ActivitySource 'LMKit.Agents')", ColorBanner);
            WriteColored(new string('=', 78), ColorBanner);
            Console.WriteLine();

            int orch  = _trace.Count(e => e.Operation == "orchestration.execute");
            int exec  = _trace.Count(e => e.Operation == "agent.execute");
            int deleg = _trace.Count(e => e.Operation == "agent.delegate");

            WriteColored($"  orchestration.execute : {orch} span(s)", ColorOrchestration);
            WriteColored($"  agent.execute         : {exec} span(s)", ColorAgent);
            WriteColored($"  agent.delegate        : {deleg} span(s)", ColorDelegate);
            Console.WriteLine();

            if (deleg > 0)
            {
                WriteColored(
                    "  agent.delegate spans confirm the SupervisorOrchestrator dispatched " +
                    "work to its workers. In a real OTLP exporter these would nest under " +
                    "the supervisor's agent.execute span, giving you a per-delegation cost " +
                    "breakdown without writing custom instrumentation.",
                    ColorMuted);
            }
            else
            {
                WriteColored(
                    "  No agent.delegate spans were captured (model decided not to delegate). " +
                    "Try a question that more obviously needs the workers, or increase " +
                    "MaxCompletionTokens to give the supervisor room to produce a tool call.",
                    ColorMuted);
            }
        }

        // ----------------------------------------------------------------------
        // UI helpers
        // ----------------------------------------------------------------------

        private static void PrintBanner()
        {
            WriteColored(new string('=', 78), ColorBanner);
            WriteColored("  Agent Telemetry with ActivitySource  (LM-Kit.NET 2026.5.2)", ColorBanner);
            WriteColored("  Distributed-trace spans for orchestration / agent / delegation", ColorBanner);
            WriteColored(new string('=', 78), ColorBanner);
            Console.WriteLine();
        }

        private static void PrintIntro()
        {
            WriteColored("This demo subscribes a System.Diagnostics.ActivityListener to:", ColorMuted);
            WriteColored($"  AgentDiagnostics.SourceName = \"{AgentDiagnostics.SourceName}\"", ColorMuted);
            Console.WriteLine();
            WriteColored("Three span kinds are emitted by the runtime:", ColorMuted);
            Console.WriteLine();
            WriteColored("  [ORCH ]  orchestration.execute   per IOrchestrator.ExecuteAsync call", ColorOrchestration);
            WriteColored("  [AGENT]  agent.execute           per Agent invocation inside an orchestration", ColorAgent);
            WriteColored("  [DELEG]  agent.delegate          per SupervisorOrchestrator delegate_to_agent call", ColorDelegate);
            Console.WriteLine();
            WriteColored(
                "In production, swap the local listener for an OpenTelemetry tracer with " +
                "AddSource(AgentDiagnostics.SourceName).AddOtlpExporter().",
                ColorMuted);
            Console.WriteLine();
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }

        private static void TryClear()
        {
            try { Console.Clear(); }
            catch (IOException) { /* stdout redirected */ }
        }

        // ----------------------------------------------------------------------
        // Model selection
        // ----------------------------------------------------------------------

        private static LM SelectModel()
        {
            Console.WriteLine("Select a model. Two scenarios share one LM instance.");
            Console.WriteLine();
            Console.WriteLine("  0 - Alibaba Qwen 3.5 9B    (~7 GB VRAM)  [Recommended]");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 4B    (~3.5 GB VRAM)");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 2B    (~2 GB VRAM)  [Fastest]");
            Console.WriteLine("  3 - Google Gemma 4 E4B     (~6 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B     (~12 GB VRAM) [Highest quality]");
            Console.WriteLine();
            Console.Write("Choice (0-4) or custom model ID/URI [default 0]: ");

            string input = (Console.ReadLine() ?? "").Trim();

            string modelId = input switch
            {
                "" or "0" => "qwen3.5:9b",
                "1" => "qwen3.5:4b",
                "2" => "qwen3.5:2b",
                "3" => "gemma4:e4b",
                "4" => "gptoss:20b",
                _ => input
            };

            Console.WriteLine();
            WriteColored($"Loading {modelId}...", ColorMuted);

            if (!modelId.Contains("://"))
            {
                return LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            return new LM(
                new Uri(modelId),
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double percent = bytesRead / (double)contentLength.Value * 100.0;
                Console.Write(
                    $"\rDownloading model: {percent,5:F1}% " +
                    $"({bytesRead / 1024.0 / 1024.0:F1} / " +
                    $"{contentLength.Value / 1024.0 / 1024.0:F1} MB) ");
            }
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading model: {progress * 100,5:F0}%   ");
            if (progress >= 1f) Console.WriteLine();
            return true;
        }
    }
}
