# Agent Telemetry with ActivitySource

Demonstrates the OpenTelemetry-compatible distributed-trace surface introduced in **LM-Kit.NET 2026.5.2** via [`AgentDiagnostics`](../../../../LM-Kit.NET/Agents/Observability/AgentDiagnostics.cs).

The agent runtime emits standard `System.Diagnostics.ActivitySource` spans for every orchestration run, every per-agent invocation, and every supervisor delegation. Subscribers register a plain `ActivityListener` (this demo) or wire the source into OpenTelemetry's `TracerProviderBuilder.AddSource(...)` for production export to Jaeger / Tempo / Datadog / Application Insights / etc.

## Features

- Subscribes an `ActivityListener` to `AgentDiagnostics.SourceName` (`"LMKit.Agents"`).
- Color-coded live span output (cyan for orchestration, green for agent, yellow for delegation, red for errors).
- Two scenarios run back-to-back:
  1. **`PipelineOrchestrator`** (`research -> write`): emits 1 `orchestration.execute` and 2 `agent.execute` spans.
  2. **`SupervisorOrchestrator`** (supervisor delegating to workers): emits 1 `orchestration.execute`, 1+ `agent.execute`, and 1+ `agent.delegate` spans.
- Per-span tags shown inline (`agent.name`, `orchestrator.name`, `orchestration.step`, `agent.planning_strategy`, `agent.inference_count`, `delegation.from`, `delegation.to`).
- Final summary counts the spans of each kind so you can verify the runtime emits what you expect.

## Spans Emitted

| Span name | When | Notable tags |
|---|---|---|
| `orchestration.execute` | Top of `IOrchestrator.ExecuteAsync` | `orchestrator.name`, `orchestration.agent_count`, `orchestration.inference_count` |
| `agent.execute` | Per-agent invocation inside an orchestration | `agent.name`, `orchestrator.name`, `orchestration.step`, `agent.planning_strategy`, `agent.status`, `agent.inference_count` |
| `agent.delegate` | Inside `DelegateTool.InvokeAsync` when a `SupervisorOrchestrator` worker is invoked | `delegation.from`, `delegation.to` |

Every span ends with an `ActivityStatusCode` of `Ok` or `Error`, so downstream tooling can alert on failure rates without parsing tags.

## Prerequisites

- .NET 8.0+
- ~6 GB VRAM for the recommended `qwen3.5:9b` model (CPU inference also works, slower).
- The model menu also offers `qwen3.5:4b`, `qwen3.5:2b`, `gemma4:e4b`, and `gptoss:20b`.

## Run

```sh
cd demos/console_net/agents/agent_telemetry_with_activity_source
dotnet run -c Release
```

## Example Output

```
==============================================================================
  Agent Telemetry with ActivitySource  (LM-Kit.NET 2026.5.2)
  Distributed-trace spans for orchestration / agent / delegation
==============================================================================

Listening on ActivitySource 'LMKit.Agents'. Spans emitted by every orchestrator/agent will appear below.

------------------------------------------------------------------------------
Scenario 1: PipelineOrchestrator (research -> write)
Expect: 1x orchestration.execute, 2x agent.execute
------------------------------------------------------------------------------

  [AGENT] agent.execute              960 ms  status=Ok
           agent.name=Researcher, orchestrator.name=Pipeline, orchestration.step=1, agent.planning_strategy=None, agent.inference_count=1
  [AGENT] agent.execute              153 ms  status=Ok
           agent.name=Writer, orchestrator.name=Pipeline, orchestration.step=2, agent.planning_strategy=None, agent.inference_count=1
  [ORCH ] orchestration.execute     1116 ms  status=Ok
           orchestrator.name=Pipeline

Final tagline:
  Edge AI inference empowers consumer devices to execute real-time machine learning tasks ...

------------------------------------------------------------------------------
Scenario 2: SupervisorOrchestrator (supervisor delegating to workers)
Expect: 1x orchestration.execute, 1+x agent.execute, 1+x agent.delegate
------------------------------------------------------------------------------

  [DELEG] agent.delegate             181 ms  status=Ok
           delegation.from=Supervisor, delegation.to=Coder
  [AGENT] agent.execute             1491 ms  status=Ok
           agent.name=Supervisor, orchestrator.name=Supervisor, orchestration.step=1, agent.planning_strategy=ReAct, agent.inference_count=1
  [ORCH ] orchestration.execute     1513 ms  status=Ok
           orchestrator.name=Supervisor
```

## Wiring Into OpenTelemetry

Replace the `ActivityListener` in this demo with a real exporter:

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using LMKit.Agents.Observability;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("my-agent-service"))
    .AddSource(AgentDiagnostics.SourceName)
    .AddOtlpExporter()       // OTLP to Jaeger, Tempo, Datadog, Application Insights
    .Build();
```

`AgentDiagnostics.SourceName` is the constant `"LMKit.Agents"`. Use it instead of a string literal so you do not drift if the name ever changes.

## How This Differs from `IAgentTracer`

LM-Kit ships two complementary observability systems:

| | `AgentDiagnostics` (this demo) | `IAgentTracer` (`InMemoryTracer`, `ConsoleTracer`, ...) |
|---|---|---|
| Granularity | Coarse spans for orchestration, agent, and delegation calls | Fine-grained per-iteration events tied to the planning loop |
| Cross-process | ✓ Distributed-trace correlation | ✗ In-process only |
| Use case | Production tracing, SLO alerting, cost dashboards | Agent-development debugging, planner tracing |

Both can run side-by-side; pick the right tool for the job.

## Related

- [Distributed tracing how-to](https://docs.lm-kit.com/lm-kit-net/guides/how-to/add-distributed-tracing-with-opentelemetry.html)
- [Graph orchestration showcase](../graph_orchestration_showcase/): pairs with this demo to show telemetry across nested orchestration shapes.
