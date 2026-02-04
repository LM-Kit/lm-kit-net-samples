# Telemetry and Observability Demo

This demo showcases LM-Kit.NET's OpenTelemetry integration for monitoring and observing LLM inference operations. Telemetry is collected in memory and displayed on demand.

## Features

- **In-Memory Telemetry Collection**: Captures traces and metrics silently using .NET's `ActivityListener` and `MeterListener`
- **On-Demand Display**: View collected telemetry only when you request it with `/traces` or `/metrics`
- **Conversation Correlation**: Uses `ConversationId` to correlate all spans within a session
- **Real-time Stats**: Shows token counts, latency, and throughput after each response

## Prerequisites

- .NET 8.0 or later
- Approximately 3-6 GB of VRAM depending on model selection

## How It Works

### 1. Telemetry Listener Configuration

The demo uses .NET's built-in `ActivityListener` and `MeterListener` to capture LM-Kit telemetry:

```csharp
using LMKit.Telemetry;
using System.Diagnostics;
using System.Diagnostics.Metrics;

// Listen to LM-Kit activities (traces)
var activityListener = new ActivityListener
{
    ShouldListenTo = source => source.Name == LMKitTelemetry.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
        ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity =>
    {
        // Store activity data for later display
        CollectActivity(activity);
    }
};
ActivitySource.AddActivityListener(activityListener);

// Listen to LM-Kit metrics
var meterListener = new MeterListener();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == LMKitTelemetry.MeterName)
    {
        listener.EnableMeasurementEvents(instrument);
    }
};
meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, state) =>
{
    // Store metric data for later display
    CollectMetric(instrument.Name, value);
});
meterListener.Start();
```

### 2. Conversation Correlation

Each `ChatHistory` instance has a unique `ConversationId` for correlating spans:

```csharp
MultiTurnConversation chat = new(model);
Console.WriteLine($"Conversation ID: {chat.ChatHistory.ConversationId}");
```

### 3. Collected Telemetry

**Metrics:**
- `gen_ai.server.time_to_first_token`: Time until first token (seconds)
- `gen_ai.server.time_per_output_token`: Average latency per token (seconds)
- `gen_ai.server.request.duration`: Total request duration (seconds)
- `gen_ai.client.token.usage`: Token counts with type tag (input/output)
- `gen_ai.client.operation.duration`: Client-side duration (seconds)

**Span Attributes:**
- `gen_ai.conversation.id`: Session correlation ID
- `gen_ai.response.id`: Unique response identifier
- `gen_ai.response.finish_reasons`: Why generation stopped (stop, length, tool_calls)
- `gen_ai.request.temperature`, `top_p`, `top_k`: Sampling parameters
- `gen_ai.request.max_tokens`: Maximum completion tokens
- `gen_ai.usage.input_tokens`, `output_tokens`: Token counts

## Usage

1. Run the demo:
   ```bash
   dotnet run
   ```

2. Select a model from the menu

3. Chat with the assistant. Telemetry is collected silently in the background

4. Use commands to view telemetry:
   - `/traces` - Display collected trace spans with their attributes
   - `/metrics` - Display collected metrics with statistics (count, sum, avg, min, max)
   - `/info` - Show telemetry configuration information
   - `/clear` - Clear collected telemetry data
   - `/reset` - Clear chat history and start a new conversation

## Example Output

```
==============================================
   LM-Kit.NET Telemetry & Observability Demo
==============================================

Conversation ID: 7a3b2c1d4e5f6a7b8c9d0e1f2a3b4c5d
(This ID correlates all telemetry spans for this session)

User: /traces

--- Collected Trace Spans ---

[1] text_completion ministral-3-3b-instruct
    Duration: 1523.45ms
    Status: Ok
    gen_ai.operation.name: text_completion
    gen_ai.provider.name: lmkit
    gen_ai.conversation.id: 7a3b2c1d4e5f6a7b8c9d0e1f2a3b4c5d
    gen_ai.response.id: 00-abc123...
    gen_ai.response.finish_reasons: stop
    gen_ai.request.temperature: 0.7
    gen_ai.usage.input_tokens: 45
    gen_ai.usage.output_tokens: 128

User: /metrics

--- Collected Metrics ---

gen_ai.client.token.usage (input):
    Count: 3
    Sum: 135.00
    Avg: 45.0000
    Min: 42.0000
    Max: 48.0000

gen_ai.client.token.usage (output):
    Count: 3
    Sum: 384.00
    Avg: 128.0000
    Min: 98.0000
    Max: 156.0000

gen_ai.server.request.duration:
    Count: 3
    Sum: 4.52
    Avg: 1.5067
    Min: 1.2340
    Max: 1.8920
```

## Integration with External Systems

To export telemetry to external systems (Jaeger, OTLP, Application Insights), replace the in-memory listeners with OpenTelemetry SDK exporters:

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Metrics;

// Configure with OTLP exporter
var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource(LMKitTelemetry.ActivitySourceName)
    .AddOtlpExporter()  // Exports to OTLP endpoint
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(LMKitTelemetry.MeterName)
    .AddOtlpExporter()
    .Build();
```
