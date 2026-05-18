# Sampler Comparison Lab

Interactive console app that runs the same prompt under four standard sampling strategies side-by-side, or under any one of them with custom parameters.

## What it shows

- `GreedyDecoding` for deterministic output.
- `RandomSampling { Temperature, TopP, MinP, TopK }` for nucleus sampling.
- `Mirostat2Sampling { Temperature, TargetEntropy, LearningRate }` for entropy-controlled output.
- `RepetitionPenalty { TokenCount, RepeatPenalty, FrequencyPenalty, PresencePenalty }`.
- Six interactive modes from a menu:
  - **Compare**: run all 4 standard samplers on one prompt.
  - **Greedy / LowTemp / HighTemp / Mirostat**: run one sampler at a time.
  - **Custom**: run RandomSampling with user-typed Temp/TopP/MinP/TopK.

## Run

```bash
cd console_net/local-inference/sampling-controls/sampler_comparison_lab
dotnet run
```

No command-line arguments. The model loads once at startup. Each pass prints answer + token count + tokens/sec so you can compare diversity vs throughput.

## Where this fits

Production tasks have different needs. Extraction / classification / command parsing want `GreedyDecoding`. Free-form writing / brainstorming want `RandomSampling`. Long-form narrative wants `Mirostat2Sampling` to avoid temperature drift. The lab makes that contrast obvious on your own prompts.
