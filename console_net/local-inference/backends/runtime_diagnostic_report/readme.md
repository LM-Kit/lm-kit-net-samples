# Hardware Backends Inspector

Reports which native backend LM-Kit picked, which GPUs the runtime sees, and how a model gets distributed across CPU and GPU at load time.

## What it shows

- `LMKit.Global.Runtime.Initialize()` triggers backend discovery.
- `Runtime.Backend` returns one of `CPU`, `Cuda12`, `Cuda13`, `Metal`, `Vulkan`, `Sycl`, `Avx`, `Avx2`.
- `Runtime.HasGpuSupport`, `Runtime.EnableCuda`, `Runtime.EnableVulkan` describe what was compiled in.
- `GpuDeviceInfo.Devices` enumerates every GPU with `DeviceName`, `TotalMemorySize`, `FreeMemorySize`.
- `LM.DeviceConfiguration.GpuLayerCount = 0` forces CPU-only loading.
- After load, `model.GpuLayerCount` reports how many layers ended up on the GPU.

## Run

```bash
cd console_net/local-inference/backends/backends
dotnet run
```

The first section prints runtime info and the GPU table. The second section loads `gemma3:270m` with auto configuration and then reloads it pinned to CPU so you can see the difference.

## Where this fits

When a customer reports "the model is slow", the first thing you need to know is whether the layers actually landed on the GPU. This demo is the diagnostic harness you can ship as part of your support tooling.
