using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Agents.Orchestration.Nodes;
using LMKit.Model;
using LMKit.TextGeneration.Chat;
using LMKit.TextGeneration.Sampling;
using System.Text;

namespace graph_orchestration_showcase
{
    /// <summary>
    /// Demonstrates the graph composition layer added in LM-Kit.NET 2026.5.2.
    ///
    /// The four prebuilt orchestrators (PipelineOrchestrator, ParallelOrchestrator,
    /// RouterOrchestrator, SupervisorOrchestrator) each express one fixed pattern.
    /// GraphOrchestrator lets you nest any combination of those patterns inside a
    /// single workflow, exposed as a tree of IOrchestrationNode instances.
    ///
    /// This demo implements a "draft -> review -> revise" pipeline. Six agents,
    /// four composition patterns, one entry point, and ONE polished answer at the
    /// end:
    ///
    ///   Sequential
    ///     |-- Classify        (custom node, writes label to State)
    ///     |-- Conditional     route to TechExpert or BizExpert -> initial draft
    ///     |-- Parallel        StyleReviewer + FactChecker review the draft
    ///     '-- Revise          custom node, rewrites the draft using both reviews
    ///                         and returns the FINAL ANSWER as the graph's output.
    ///
    /// State carries cross-stage data (original question, expert's draft) so the
    /// final reviser sees everything it needs without polluting any agent's input.
    /// </summary>
    internal static class Program
    {
        private const string LicenseKey = "";

        // Per-role console color so the user can see which agent is producing what.
        private const ConsoleColor ColorClassifier   = ConsoleColor.Cyan;
        private const ConsoleColor ColorTechExpert   = ConsoleColor.Yellow;
        private const ConsoleColor ColorBizExpert    = ConsoleColor.Magenta;
        private const ConsoleColor ColorStyleReviewer= ConsoleColor.Green;
        private const ConsoleColor ColorFactChecker  = ConsoleColor.Red;
        private const ConsoleColor ColorReviser      = ConsoleColor.Blue;
        private const ConsoleColor ColorBanner       = ConsoleColor.White;
        private const ConsoleColor ColorMuted        = ConsoleColor.DarkGray;
        private const ConsoleColor ColorFinal        = ConsoleColor.White;

        // Shared state keys used across nodes.
        internal const string StateClassification = "classification";
        internal const string StateExpertDraft    = "expert_draft";

        private static bool _isDownloading;

        private static async Task Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey(LicenseKey);
            Console.OutputEncoding = Encoding.UTF8;
            try { Console.InputEncoding = Encoding.UTF8; } catch { /* not interactive */ }
            TryClear();

            PrintBanner();
            PrintIntro();

            using var model = SelectModel();

            TryClear();
            PrintBanner();

            WriteColored("Building the graph...", ColorMuted);
            Console.WriteLine();

            var (graph, agents) = BuildGraph(model);
            var orchestrator = new GraphOrchestrator(graph, name: "showcase");

            WireProgressEvents(orchestrator, agents);

            // Per-orchestration options propagate to every agent in the graph.
            // Greedy decoding gives reproducible answers; we cap completion tokens
            // tightly because each agent has a small, focused job.
            var options = new OrchestrationOptions
            {
                SamplingMode = new GreedyDecoding(),
                MaxCompletionTokens = 256,
                ReasoningLevel = ReasoningLevel.None,
                EnableTracing = true,
                StopOnFailure = true
            };

            string[] questions =
            {
                "How should I version a public REST API?",
                "What pricing model maximizes lifetime value for a B2B SaaS?"
            };

            foreach (string question in questions)
            {
                Console.WriteLine();
                WriteColored(new string('-', 78), ColorBanner);
                WriteColored($"QUESTION: {question}", ColorBanner);
                WriteColored(new string('-', 78), ColorBanner);
                Console.WriteLine();

                var startedAt = DateTimeOffset.UtcNow;
                var result = await orchestrator.ExecuteAsync(question, options);
                var elapsed = DateTimeOffset.UtcNow - startedAt;

                Console.WriteLine();

                if (!result.Success)
                {
                    WriteColored($"FAILED: {result.Error}", ConsoleColor.Red);
                    continue;
                }

                PrintFinalSummary(result, elapsed);
            }

            if (!Console.IsInputRedirected)
            {
                Console.WriteLine();
                WriteColored("Press Enter to exit.", ColorMuted);
                Console.ReadLine();
            }
        }

        private static void TryClear()
        {
            try { Console.Clear(); }
            catch (IOException) { /* stdout redirected */ }
        }

        // ----------------------------------------------------------------------
        // Graph construction
        // ----------------------------------------------------------------------

        private record AgentSet(
            Agent Classifier,
            Agent TechExpert,
            Agent BizExpert,
            Agent StyleReviewer,
            Agent FactChecker,
            Agent Reviser);

        private static (IOrchestrationNode graph, AgentSet agents) BuildGraph(LM model)
        {
            var classifier = Agent.CreateBuilder(model)
                .WithPersona("Classifier")
                .WithInstruction(
                    "You classify a question as 'tech' (software, APIs, infrastructure, " +
                    "code) or 'biz' (business, marketing, pricing, strategy). Reply with " +
                    "exactly one lowercase word: 'tech' or 'biz'. Nothing else.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // The experts are deliberately biased to produce a *flawed* first draft so
            // the reviewers have something real to critique and the Reviser can show
            // a visible improvement. They are forced to include:
            //   1. A verbose, corporate-speak opening phrase (style issue)
            //   2. A specific made-up percentage statistic (fact issue)
            //
            // This is the standard SDLC of a real reviewing pipeline: the first draft
            // is rarely clean and the value of the review/revise loop is in catching
            // exactly these kinds of issues.
            var techExpert = Agent.CreateBuilder(model)
                .WithPersona("TechExpert")
                .WithInstruction(
                    "You are a senior software engineer. Answer the user's question in " +
                    "4 to 5 sentences. Always begin your answer with the exact phrase " +
                    "'It is widely accepted in modern software engineering that'. " +
                    "Always include at least one specific industry statistic with a " +
                    "percentage and a year (for example '87% of teams in 2023...'). " +
                    "Be assertive and avoid hedging.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var bizExpert = Agent.CreateBuilder(model)
                .WithPersona("BizExpert")
                .WithInstruction(
                    "You are a business strategist. Answer the user's question in 4 to " +
                    "5 sentences. Always begin your answer with the exact phrase " +
                    "'According to comprehensive industry research,'. Always include " +
                    "at least one specific percentage figure (for example '74% " +
                    "improvement in customer retention'). Be confident and avoid hedging.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var styleReviewer = Agent.CreateBuilder(model)
                .WithPersona("StyleReviewer")
                .WithInstruction(
                    "Review the draft answer below for clarity and tone. Look " +
                    "specifically for verbose corporate-speak openings, unnecessary " +
                    "filler phrases, and overconfident absolutes. Reply with a single " +
                    "sentence pointing out ONE specific style improvement, or 'No " +
                    "style issues.' if the prose is already clean.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var factChecker = Agent.CreateBuilder(model)
                .WithPersona("FactChecker")
                .WithInstruction(
                    "Review the draft answer below for factual accuracy. Look " +
                    "specifically for unsupported statistics, fabricated percentages, " +
                    "and overconfident claims that lack a citation. Reply with a " +
                    "single sentence pointing out ONE specific claim that should be " +
                    "verified or removed, or 'No factual issues.' if all claims are " +
                    "well-supported.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // The reviser is the agent the user actually reads at the end. It receives
            // the original question, the expert's draft, and the reviewers' feedback
            // (composed by ReviseDraftNode below) and produces ONE polished final answer.
            var reviser = Agent.CreateBuilder(model)
                .WithPersona("Reviser")
                .WithInstruction(
                    "You are a senior editor. You will receive a question, an initial " +
                    "draft answer, and reviewer feedback. Produce a single polished " +
                    "final answer that incorporates the feedback: drop verbose " +
                    "openers, remove or qualify any unsupported statistics flagged by " +
                    "the fact checker, and tighten the prose. Reply with ONLY the " +
                    "final answer, no preamble, no headings, no bullet list of changes.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // Composite shape no single prebuilt orchestrator can express. Six agents,
            // four nesting patterns, one entry point, ONE polished answer at the end.
            //
            //   Sequential[
            //     ClassifyAndPreserve,        runs classifier, stores the label in
            //                                 State, forwards the ORIGINAL question
            //     Conditional(tech|biz),      routes to expert wrapped in a
            //                                 CaptureExpertDraftNode that stashes the
            //                                 expert's output in State for the reviser
            //     Parallel[style, facts],     two reviewers concurrently critique the
            //                                 draft (the expert's output flows in)
            //     ReviseDraft                 reads question + draft + reviewer
            //                                 feedback and emits the final answer
            //   ]
            IOrchestrationNode graph = new SequentialNode("flow",
                new ClassifyAndPreserveNode("classify", classifier),
                new CaptureExpertDraftNode(
                    new ConditionalNode("route",
                        selector: ctx => ctx.Orchestration.GetState(StateClassification, "biz"),
                        branches: new Dictionary<string, IOrchestrationNode>
                        {
                            ["tech"] = new AgentNode("tech", techExpert),
                            ["biz"] = new AgentNode("biz", bizExpert)
                        },
                        defaultBranch: new AgentNode("biz-default", bizExpert))),
                new ParallelNode("review",
                    new IOrchestrationNode[]
                    {
                        new AgentNode("style", styleReviewer),
                        new AgentNode("facts", factChecker)
                    }),
                new ReviseDraftNode("revise", reviser));

            var agents = new AgentSet(classifier, techExpert, bizExpert, styleReviewer, factChecker, reviser);
            return (graph, agents);
        }

        private static void WireProgressEvents(GraphOrchestrator orchestrator, AgentSet agents)
        {
            // Wrap long inputs/outputs across multiple lines instead of truncating, so the
            // user can verify exactly what is flowing between agents. The full chain is
            // also reprinted in the final summary; this live view is the per-step trace.
            const int wrap = 110;

            orchestrator.BeforeAgentExecution += (s, e) =>
            {
                var color = ColorFor(e.Agent, agents);
                WriteColored(
                    $"  [start ] {e.Agent.Identity?.Persona,-13} <- INPUT ({(e.OriginalInput?.Length ?? 0)} chars)",
                    color);
                foreach (var line in WrapText(e.OriginalInput, wrap))
                {
                    WriteColored("           | " + line, color);
                }
            };

            orchestrator.AfterAgentExecution += (s, e) =>
            {
                var color = ColorFor(e.Agent, agents);
                WriteColored(
                    $"  [finish] {e.Agent.Identity?.Persona,-13} -> OUTPUT ({(e.Result.Content?.Length ?? 0)} chars)",
                    color);
                foreach (var line in WrapText(e.Result.Content, wrap))
                {
                    WriteColored("           | " + line, color);
                }
                WriteColored(
                    $"           status={e.Result.Status} inferences={e.Result.InferenceCount}",
                    ColorMuted);
                Console.WriteLine();
            };
        }

        // Word-wrap a possibly-multiline string so we can print full content per stage
        // without runaway line lengths or losing content via truncation.
        private static IEnumerable<string> WrapText(string text, int width)
        {
            if (string.IsNullOrEmpty(text)) yield break;

            // Normalize internal whitespace so line breaks come from our wrapping pass.
            text = text.Replace("\r\n", "\n").Replace("\r", "\n");

            foreach (var paragraph in text.Split('\n'))
            {
                if (paragraph.Length == 0) { yield return string.Empty; continue; }

                int i = 0;
                while (i < paragraph.Length)
                {
                    int len = Math.Min(width, paragraph.Length - i);
                    if (i + len < paragraph.Length)
                    {
                        // Try to break on a space within the last 25 chars of the chunk.
                        int lastSpace = paragraph.LastIndexOf(' ', i + len - 1, Math.Min(25, len));
                        if (lastSpace > i) len = lastSpace - i;
                    }
                    yield return paragraph.Substring(i, len).TrimEnd();
                    i += len;
                    while (i < paragraph.Length && paragraph[i] == ' ') i++;
                }
            }
        }

        private static ConsoleColor ColorFor(Agent agent, AgentSet agents)
        {
            if (agent == agents.Classifier)    return ColorClassifier;
            if (agent == agents.TechExpert)    return ColorTechExpert;
            if (agent == agents.BizExpert)     return ColorBizExpert;
            if (agent == agents.StyleReviewer) return ColorStyleReviewer;
            if (agent == agents.FactChecker)   return ColorFactChecker;
            if (agent == agents.Reviser)       return ColorReviser;
            return ColorBanner;
        }

        // ----------------------------------------------------------------------
        // Output helpers
        // ----------------------------------------------------------------------

        private static void PrintBanner()
        {
            WriteColored(new string('=', 78), ColorBanner);
            WriteColored("  Graph Orchestration Showcase  (LM-Kit.NET 2026.5.2)", ColorBanner);
            WriteColored("  draft -> review -> revise pipeline", ColorBanner);
            WriteColored(new string('=', 78), ColorBanner);
            Console.WriteLine();
        }

        private static void PrintIntro()
        {
            WriteColored("This demo composes 6 agents in a draft -> review -> revise pipeline.", ColorMuted);
            WriteColored("Each question produces ONE polished final answer:", ColorMuted);
            Console.WriteLine();
            WriteColored("  1) Classifier      labels the question 'tech' or 'biz'", ColorClassifier);
            WriteColored("  2) Conditional     routes to TechExpert (yellow) or BizExpert (magenta)", ColorTechExpert);
            WriteColored("                     -> produces an initial draft answer", ColorBizExpert);
            WriteColored("  3) Parallel        StyleReviewer (green) and FactChecker (red)", ColorStyleReviewer);
            WriteColored("                     review the draft concurrently", ColorFactChecker);
            WriteColored("  4) Reviser (blue)  rewrites the draft using the reviewers' feedback", ColorReviser);
            WriteColored("                     -> produces the FINAL ANSWER", ColorReviser);
            Console.WriteLine();
        }

        private static void PrintFinalSummary(OrchestrationResult result, TimeSpan elapsed)
        {
            const int wrap = 110;

            // The graph's final output is the Reviser's polished answer.
            // result.Content is, by definition, the last node's output, which here is
            // the ReviseDraftNode's output (the polished final answer).
            WriteColored(new string('=', 78), ColorFinal);
            WriteColored("  FINAL ANSWER (Reviser, polished using Style + Facts feedback)", ColorFinal);
            WriteColored(new string('=', 78), ColorFinal);
            Console.WriteLine();

            string finalAnswer = (result.Content ?? string.Empty).Trim();
            foreach (var line in WrapText(finalAnswer, wrap))
            {
                WriteColored("  " + line, ColorFinal);
            }

            Console.WriteLine();
            WriteColored(new string('-', 78), ColorMuted);
            WriteColored("  How this answer was produced (per-stage transcript):", ColorMuted);
            WriteColored(new string('-', 78), ColorMuted);
            Console.WriteLine();

            // Per-stage transcript so the user can audit how the final answer was built.
            // AgentResults preserves graph-execution order: classify, tech|biz draft,
            // style + facts (parallel), revise.
            foreach (var step in result.AgentResults)
            {
                var stepColor = ColorForAgentName(step.AgentName);
                WriteColored($"  [{step.AgentName}] ({(step.Content?.Length ?? 0)} chars)", stepColor);
                foreach (var line in WrapText(step.Content, wrap))
                {
                    WriteColored("    " + line, stepColor);
                }
                Console.WriteLine();
            }

            WriteColored(
                $"  ({result.AgentResults.Count} agents executed in {elapsed.TotalSeconds:F1}s, " +
                $"{result.TotalInferenceCount} total inferences)",
                ColorMuted);
        }

        private static ConsoleColor ColorForAgentName(string nodeName)
        {
            return nodeName switch
            {
                "classify"     => ColorClassifier,
                "tech"         => ColorTechExpert,
                "biz"          => ColorBizExpert,
                "biz-default"  => ColorBizExpert,
                "style"        => ColorStyleReviewer,
                "facts"        => ColorFactChecker,
                "revise"       => ColorReviser,
                _              => ColorBanner
            };
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ForegroundColor = prev;
        }

        // ----------------------------------------------------------------------
        // Model selection
        // ----------------------------------------------------------------------

        private static LM SelectModel()
        {
            Console.WriteLine("Select a model. The whole graph (5 agents) shares one LM instance.");
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

    /// <summary>
    /// Custom IOrchestrationNode that runs a classifier agent and writes its
    /// label to <see cref="OrchestrationContext"/>.State, then forwards the
    /// ORIGINAL input downstream so the routed expert sees the actual question
    /// (not the classifier's one-word label).
    /// </summary>
    /// <remarks>
    /// Graphs are not limited to AgentNode wrappers. Any IOrchestrationNode
    /// implementation plugs in the same way - this class is the canonical
    /// "agent + side-effect into shared state" pattern.
    /// </remarks>
    internal sealed class ClassifyAndPreserveNode : IOrchestrationNode
    {
        private readonly string _name;
        private readonly Agent _classifier;

        public string Name => _name;

        public ClassifyAndPreserveNode(string name, Agent classifier)
        {
            _name = name;
            _classifier = classifier;
        }

        public async Task<NodeResult> InvokeAsync(NodeContext context, CancellationToken cancellationToken)
        {
            string input = context.Input;

            // NodeContext.ExecuteAgentAsync routes through the host orchestrator
            // when one is present, so BeforeAgentExecution / AfterAgentExecution
            // fire and trace spans are emitted exactly as they would for an
            // AgentNode. The result is also recorded under our node name in the
            // shared OrchestrationContext.Results list.
            var result = await context.ExecuteAgentAsync(
                _classifier, input, resultName: _name, cancellationToken)
                .ConfigureAwait(false);

            if (result == null || result.Status != AgentExecutionStatus.Completed)
            {
                return NodeResult.Failed(
                    result?.Error ?? new InvalidOperationException(
                        $"Classifier '{_name}' returned non-completed status: {result?.Status}"),
                    result);
            }

            // Normalize the classifier output to a routing label and stash it in State.
            string raw = (result.Content ?? string.Empty).Trim().ToLowerInvariant();
            string label = raw.StartsWith("tech") ? "tech" : "biz";
            context.Orchestration.SetState(Program.StateClassification, label);
            context.Orchestration.AddTrace($"classification: {label}");

            // Forward the ORIGINAL input, not the label, so downstream agents
            // get the actual question.
            return NodeResult.Success(input, result);
        }
    }

    /// <summary>
    /// Wraps an inner node, runs it, captures its successful output into
    /// <see cref="OrchestrationContext"/>.State so a downstream node can read it,
    /// then forwards the same output to the next stage. Used here to stash the
    /// expert's initial draft so the Reviser can see it later.
    /// </summary>
    /// <remarks>
    /// This is the canonical "decorator" pattern for graph nodes: a composite
    /// that adds an observation side-effect without altering the inner node's
    /// behavior or input/output contract.
    /// </remarks>
    internal sealed class CaptureExpertDraftNode : IOrchestrationNode
    {
        private readonly IOrchestrationNode _inner;

        public string Name => _inner.Name;

        public CaptureExpertDraftNode(IOrchestrationNode inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public async Task<NodeResult> InvokeAsync(NodeContext context, CancellationToken cancellationToken)
        {
            var result = await _inner.InvokeAsync(context, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess)
            {
                context.Orchestration.SetState(Program.StateExpertDraft, result.Output ?? string.Empty);
                context.Orchestration.AddTrace(
                    $"expert draft captured ({(result.Output?.Length ?? 0)} chars)");
            }

            return result;
        }
    }

    /// <summary>
    /// Final stage of the pipeline: composes the original question, the expert's
    /// initial draft, and the reviewers' feedback into a single prompt for the
    /// Reviser agent, then returns the polished answer as the graph's output.
    /// </summary>
    /// <remarks>
    /// The reviewers' aggregated feedback arrives as <see cref="NodeContext.Input"/>
    /// (this node sits after the ParallelNode in the SequentialNode). The original
    /// question lives on <see cref="OrchestrationContext.OriginalInput"/>, and the
    /// expert's draft is read from State (set by <see cref="CaptureExpertDraftNode"/>).
    /// </remarks>
    internal sealed class ReviseDraftNode : IOrchestrationNode
    {
        private readonly string _name;
        private readonly Agent _reviser;

        public string Name => _name;

        public ReviseDraftNode(string name, Agent reviser)
        {
            _name = name;
            _reviser = reviser ?? throw new ArgumentNullException(nameof(reviser));
        }

        public async Task<NodeResult> InvokeAsync(NodeContext context, CancellationToken cancellationToken)
        {
            string question = context.Orchestration.OriginalInput ?? string.Empty;
            string draft = context.Orchestration.GetState<string>(Program.StateExpertDraft) ?? string.Empty;
            string reviewerFeedback = context.Input ?? string.Empty;

            // Compose a single user message containing every piece the reviser needs.
            // The Reviser's system prompt instructs it to reply with ONLY the polished
            // answer, no preamble, so this content becomes the graph's final output.
            string composedPrompt =
                "QUESTION:\n" + question.Trim() + "\n\n" +
                "INITIAL DRAFT (by an expert):\n" + draft.Trim() + "\n\n" +
                "REVIEWER FEEDBACK (style and facts, run in parallel):\n" + reviewerFeedback.Trim() + "\n\n" +
                "Produce the polished final answer.";

            var result = await context.ExecuteAgentAsync(
                _reviser, composedPrompt, resultName: _name, cancellationToken)
                .ConfigureAwait(false);

            if (result == null || result.Status != AgentExecutionStatus.Completed)
            {
                return NodeResult.Failed(
                    result?.Error ?? new InvalidOperationException(
                        $"Reviser '{_name}' returned non-completed status: {result?.Status}"),
                    result);
            }

            // The graph's final output is the reviser's polished answer.
            return NodeResult.Success(result.Content ?? string.Empty, result);
        }
    }
}
