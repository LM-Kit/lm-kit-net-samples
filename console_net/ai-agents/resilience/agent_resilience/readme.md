# Agent Resilience Policies Lab

Interactive console app demonstrating the four core resilience policies on a controllable flaky operation: `RetryPolicy`, `CircuitBreakerPolicy`, `FallbackPolicy<T>`, `CompositePolicy`. Run each policy on demand or replay all four end-to-end.

## What it shows

- `RetryPolicy(maxRetries)` with `.WithExponentialBackoff(initial, multiplier, maxDelay)`, `.WithJitter(factor)`, `.OnRetry(callback)`.
- `CircuitBreakerPolicy(failureThreshold, recoveryTime)` with `.OnStateChange(callback)`.
- `FallbackPolicy<T>.Create(fallbackValue, shouldHandle)`.
- `CompositePolicy().Wrap(policy1).Wrap(policy2)` for chained policies.
- Five interactive modes from a menu:
  - **Retry / Breaker / Fallback / Composite**: run that policy alone.
  - **All**: run all four back-to-back.

## Run

```bash
cd console_net/ai-agents/resilience/agent_resilience
dotnet run
```

No command-line arguments. No model load required. The demo lets you choose the failures-before-success profile interactively so you can see retries succeed or exhaust.

## Where this fits

Production agents fail under load. Retry covers transient errors, circuit-breaker stops the bleed when a downstream is dead, fallback returns a safe default, composite chains them. Declarative, testable, no try/catch sprawl.
