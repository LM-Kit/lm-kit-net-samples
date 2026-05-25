# LM-Kit.NET Samples

Runnable C# examples for [LM-Kit.NET](https://lm-kit.com/products/lm-kit-net/), the local-first AI SDK for .NET.

Every sample runs offline, on your own hardware, with no API keys and no cloud calls. Clone, `dotnet run`, done.

> New to LM-Kit? Start at [lm-kit.com](https://lm-kit.com) for the product overview, or jump straight into a sample below.

## Browse by capability

Samples are organized by LM-Kit capability pillar. Open any folder for runnable projects and per-sample readmes.

| Pillar | What you'll find |
|---|---|
| [AI Agents](console_net/ai-agents/) | Agent orchestration, planning strategies, tools, function calling, MCP, multi-agent workflows, memory, skills |
| [Document Intelligence](console_net/document-intelligence/) | PDF chat, classification, splitting, summarization, structured field extraction, document-to-markdown |
| [Vision & Multimodal](console_net/vision/) | Image chat, image embeddings, document OCR (traditional and VLM-based), document layout extraction |
| [RAG & Knowledge](console_net/rag-and-knowledge/) | RAG chat, vector databases, data extraction pipelines |
| [Text Analysis](console_net/text-analysis/) | Classification, NER, PII, sentiment, keyword extraction, language detection |
| [Speech & Audio](console_net/speech/) | Real-time and batch speech-to-text |
| [Text Generation](console_net/text-generation/) | Conversations, translation, summarization, prompt templates, grammar correction |
| [Local Inference](console_net/local-inference/) | Encrypted models, runtime configuration |
| [Model Optimization](console_net/model-optimization/) | Quantization, fine-tuning |
| [Integrations](console_net/integrations/) | Microsoft.Extensions.AI, Semantic Kernel |

## Run a sample

```bash
git clone https://github.com/LM-Kit/lm-kit-net-samples.git
cd lm-kit-net-samples/console_net/<pillar>/<sub-category>/<sample>
dotnet run
```

Each sample:
- Targets .NET 8 or later
- Pulls the LM-Kit.NET NuGet package automatically
- Downloads its model on first run (cached for later runs)
- Picks the fastest backend available on your machine (CUDA, Vulkan, Metal, or CPU)

No license key, no signup, no configuration. When you're ready to build your own app, grab a [free Community Edition license](https://lm-kit.com/products/community-edition/).

## Hardware

LM-Kit auto-selects the best backend at startup. You don't configure anything.

- NVIDIA, AMD, Intel GPUs
- Apple Silicon (Metal)
- Multi-GPU and hybrid CPU+GPU for models larger than VRAM
- CPU-only fallback with AVX/AVX2

Sample readmes list VRAM expectations where it matters.

## Learn more

- [Documentation](https://docs.lm-kit.com)
- [Model catalog](https://docs.lm-kit.com/lm-kit-net/guides/getting-started/model-catalog.html)
- [Changelog](https://docs.lm-kit.com/lm-kit-net/guides/changelog.html)
- [Blog](https://lm-kit.com/blog/)

© LM-Kit
