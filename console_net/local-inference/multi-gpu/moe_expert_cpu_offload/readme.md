# MoE Expert CPU Offload

Interactive console app that fits a 30B-class MoE model across CPU + GPU by pinning the right tensors to the right device. The pattern is what makes 30B MoE inference practical on a single consumer GPU.

## What it shows

- `Configuration.FavorDistributedInference = true` biases the loader toward splitting tensors across visible GPUs.
- `LM.TensorOverride.Cpu(regex)` and `LM.TensorOverride.Gpu(regex, gpuIndex)` for per-tensor placement.
- Default override list:
  - `\.ffn_.*_exps\.weight` -> CPU (MoE experts only see a small slice of tokens, PCIe is fine).
  - `blk\.(0|1|2)\.attn` -> GPU 0 (hot attention stays on the fastest device).
- `LM.DeviceConfiguration.AutoFitToVram = true` lets the loader retry with fewer GPU layers on OOM.
- Three interactive modes from a menu:
  - **Load**: pick an MoE model and load with the override list.
  - **Chat**: free-form prompt on the loaded model (streamed).
  - **Bench**: run the standard prompt N times and report mean throughput.

## Run

```bash
cd console_net/local-inference/multi-gpu/moe_expert_cpu_offload
dotnet run
```

No command-line arguments. Model picker offers gptoss:20b, qwen3.6:35b-a3b, qwen3.5:35b-a3b, gemma4:26b-a4b, glm4.7-flash, or any custom id.

## Where this fits

A 24 GB consumer GPU cannot hold a 30B MoE model in VRAM. With tensor overrides you can ship that model anyway: keep the hot tensors on GPU and the cold expert tensors on CPU. This is what makes local inference practical on prosumer hardware.
