# LoRA Adapter Hot-Swap

Interactive console app that loads one base model and swaps multiple LoRA adapters in and out at inference time. Apply, remove, list, compare; the persona shift is visible in sequence.

## What it shows

- `LoraAdapterSource.ValidateFormat(path, throwException)` for a fast pre-check.
- `LM.ApplyLoraAdapter(LoraAdapterSource)` to hot-load an adapter.
- `LM.Adapters` enumeration with `LoraAdapter.{Path, Scale, Identifier}` per active adapter.
- `LM.RemoveLoraAdapter(LoraAdapter)` to remove one adapter while others remain active.
- Five interactive modes from a menu:
  - **Compare**: paste adapters + prompt; runs baseline and each adapter cleanly.
  - **Apply**: apply one adapter at a chosen scale.
  - **Remove**: remove a single adapter or all.
  - **List**: show currently-applied adapters.
  - **Chat**: free-form prompt with whatever adapters are active.

## Run

```bash
cd console_net/model-optimization/lora-integration/lora_adapter_hot_swap
dotnet run
```

No command-line arguments. The base model (`qwen3.5:0.8b`) loads once at startup. Produce LoRA `.gguf` files via the [LoRA Fine-Tuning](../../llm-finetuning/lora_finetuning) demo, or bring your own.

## Where this fits

A multi-tenant SaaS that serves N customer personas does not need N base models. Keep one base in VRAM, ship per-tenant LoRA adapters of a few hundred KB each, hot-swap per request. Storage shrinks by orders of magnitude, latency is unchanged.
