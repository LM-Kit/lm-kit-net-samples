# Graph Orchestration Showcase

Demonstrates the `LMKit.Agents.Orchestration.Nodes` graph composition API introduced in **LM-Kit.NET 2026.5.2**.

The four prebuilt orchestrators (`PipelineOrchestrator`, `ParallelOrchestrator`, `RouterOrchestrator`, `SupervisorOrchestrator`) each express one fixed pattern. The graph layer lets you nest any combination of those patterns inside a single workflow, with custom `IOrchestrationNode` instances slotted in wherever non-agent work belongs.

This demo implements a useful **draft -> review -> revise** pipeline that takes a user question and produces ONE polished final answer.

## Features

- `IOrchestrationNode` as the unit of composition.
- `AgentNode`, `SequentialNode`, `ParallelNode`, `ConditionalNode` primitives.
- Three custom `IOrchestrationNode` implementations:
  - `ClassifyAndPreserveNode`: runs the classifier, writes its label to `OrchestrationContext.State`, and forwards the original question downstream so the expert sees the actual question (not the label).
  - `CaptureExpertDraftNode`: a decorator that wraps the routing branch, captures the expert's draft into `State` for the Reviser to read later, then passes it through unchanged.
  - `ReviseDraftNode`: composes the original question + expert draft + reviewer feedback into a single prompt for the Reviser agent and emits the polished final answer as the graph's output.
- `GraphOrchestrator` host that runs the graph as a regular `IOrchestrator`.
- Per-orchestration options propagated uniformly to every agent (`MaxCompletionTokens`, `ReasoningLevel`, `SamplingMode`).
- `BeforeAgentExecution` and `AfterAgentExecution` lifecycle events fire for every agent in the graph (including agents invoked from custom nodes via `NodeContext.ExecuteAgentAsync`).
- Color-coded console output: each role has its own color so you can see who is doing what.
- Live `[start ]` / `[finish]` progress with full input/output content, char counts, and word-wrapped lines.

## Prerequisites

- .NET 8.0+
- ~6 GB VRAM for the recommended `qwen3.5:9b` model (CPU inference also works, slower).
- The model menu also offers `qwen3.5:4b`, `qwen3.5:2b`, `gemma4:e4b`, and `gptoss:20b`.

## How It Works

```
Sequential("flow")
├── ClassifyAndPreserveNode("classify", Classifier)
│       writes State["classification"] = "tech" | "biz"
│       forwards the ORIGINAL question
├── CaptureExpertDraftNode( ConditionalNode("route") )
│   ├── tech : AgentNode("tech", TechExpert)
│   └── biz  : AgentNode("biz",  BizExpert)
│       writes State["expert_draft"] = expert's output
│       forwards the expert's draft to the next stage
├── ParallelNode("review")
│   ├── AgentNode("style", StyleReviewer)
│   └── AgentNode("facts", FactChecker)
│       both reviewers see the expert's draft concurrently
│       outputs are aggregated as the parallel node's result
└── ReviseDraftNode("revise", Reviser)
        reads OriginalInput + State["expert_draft"] + reviewer feedback
        produces the polished FINAL ANSWER
```

Six agents, four composition patterns, one entry point, one polished answer at the end. The flow:

1. Classifier labels the question as `tech` or `biz`.
2. Conditional routes to TechExpert or BizExpert, who writes an initial draft.
3. StyleReviewer and FactChecker review the draft in parallel.
4. Reviser rewrites the draft using both reviewers' feedback and outputs the final answer.

`OrchestrationContext.State` carries cross-stage data (the classification label and the expert's draft) so the final reviser sees everything it needs without polluting any agent's input.

## Run

```sh
cd demos/console_net/agents/graph_orchestration_showcase
dotnet run -c Release
```

A model picker prompts on startup; the same `LM` instance is shared by all 6 agents.

## Example Output

```
==============================================================================
  Graph Orchestration Showcase  (LM-Kit.NET 2026.5.2)
  draft -> review -> revise pipeline
==============================================================================

------------------------------------------------------------------------------
QUESTION: How should I version a public REST API?
------------------------------------------------------------------------------

  [start ] Classifier    <- INPUT (39 chars)
           | How should I version a public REST API?
  [finish] Classifier    -> OUTPUT (4 chars)
           | tech

  [start ] TechExpert    <- INPUT (39 chars)
           | How should I version a public REST API?
  [finish] TechExpert    -> OUTPUT (938 chars)
           | To version a public REST API effectively, you should ... [draft]

  [start ] StyleReviewer <- INPUT (938 chars)
           | [the full draft]
  [finish] StyleReviewer -> OUTPUT (213 chars)
           | [style suggestion]

  [start ] FactChecker   <- INPUT (938 chars)
           | [the full draft]
  [finish] FactChecker   -> OUTPUT (159 chars)
           | The draft answer repeats the instruction to include a `Deprecation` header twice ...

  [start ] Reviser       <- INPUT (1490 chars)
           | QUESTION: ...
           | INITIAL DRAFT: ...
           | REVIEWER FEEDBACK: ...
  [finish] Reviser       -> OUTPUT (749 chars)
           | [polished final answer with the redundant sentence removed]

==============================================================================
  FINAL ANSWER (Reviser, polished using Style + Facts feedback)
==============================================================================

  To version a public REST API effectively, you should implement a versioning
  scheme that allows clients to specify their preferred API version, such as
  using the `api/v1` prefix in the URL path. ...
```

## Extending the Demo

- Replace one `AgentNode` with a `SupervisorOrchestrator` wrapped in a custom node. Its delegations also emit `agent.delegate` activities and respect the orchestration's options.
- Add a fourth reviewer in the `ParallelNode` (security audit, cost analysis, etc.). The graph absorbs it without any other change.
- Implement another `IOrchestrationNode` for non-agent work (DB lookup, validation) and slot it in where it belongs.
- Wire `LMKit.Agents.Observability.AgentDiagnostics.SourceName` into your tracer to capture every `agent.execute` and `orchestration.execute` span. See the [Agent Telemetry showcase](../agent_telemetry_with_activity_source/) demo.

See [the graph composition guide](https://docs.lm-kit.com/lm-kit-net/guides/how-to/compose-orchestrations-with-graph-nodes.html) for design notes.
