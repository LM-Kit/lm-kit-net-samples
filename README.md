# Local AI Agent Platform for .NET Developers

> **Your AI. Your Data. On Your Device.**

**LM-Kit.NET** is the only full-stack AI framework for .NET that unifies everything you need to build and deploy AI agents with zero cloud dependency. It combines the fastest .NET inference engine, production-ready trained models, agent orchestration, RAG pipelines, and MCP-compatible tool calling in a single in-process SDK for C# and VB.NET.

🔒 **100% Local** · ⚡ **No Signup** · 🌐 **Cross-Platform** · 📦 **Zero Dependencies**

---

### 🎉 Claim Your Free Community License!

Get started with the **LM-Kit Community Edition** today. Whether you're a hobbyist, startup, or open-source developer, the Community Edition provides full access to build and experiment.

👉 [Claim Your Free License Now!](https://lm-kit.com/products/community-edition/)

---

## Built for Performance, Engineered for Innovation

LM-Kit.NET is a highly technical SDK that brings cutting-edge AI research directly into the .NET ecosystem. Our engineering team continuously ships the latest advances in generative AI, symbolic AI, and NLP - with weekly releases that add new model architectures, optimize inference pipelines, and expand capabilities across the entire stack.

Check our [changelog](https://docs.lm-kit.com/lm-kit-net/guides/changelog.html) to see the pace of innovation.

---

## 🚀 What's New

*Listed from most recent to oldest*

- 📄 **[PDF Chat + Document RAG](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Retrieval.PdfChat.html)** - Import PDFs, index them locally, and chat with them using the new `DocumentRag` and `PdfChat` APIs (with built-in PDF attachments, chunking, and a local vector store)
- 🔧 **[Tool Calling for Local Agents](https://lm-kit.com/blog/tool-calling-for-local-agents/)** - Build AI agents with state-of-the-art tool calling. Supports all modes (simple, multiple, parallel) with structured JSON schemas, safety policies, and human-in-the-loop controls
- 🔗 **[MCP Client Support](https://docs.lm-kit.com/lm-kit-net/api/LMKit.Agents.McpClient.html)** - Connect agents to Model Context Protocol servers for extended capabilities including resources, prompts, and tool discovery
- 🎙️ **[Speech-to-Text](https://lm-kit.com/solutions/language-processing/speech-to-text/)** - Convert spoken audio into highly accurate text transcripts with voice activity detection, supporting 100+ languages
- 👁️ **[VLM-Based OCR](https://docs.lm-kit.com/lm-kit-net/api/LMKit.TextGeneration.VlmOcr.html)** - High-accuracy text extraction from images and scanned documents using vision language models
- 🛡️ **[Multimodal PII Extraction](https://lm-kit.com/solutions/content-analysis/#pii-extraction)** - Identify and extract personally identifiable information from text and images for compliance
- 🏷️ **[Multimodal Named Entity Recognition](https://lm-kit.com/solutions/content-analysis/#ner)** - Detect and classify entities (people, organizations, locations, etc.) across text and images
- 🌐 **Multimodal RAG with Reranking** - Improve accuracy with multimodal retrieval-augmented generation and semantic reranking
- 🧬 **[Built-in Vector Database Engine](https://lm-kit.com/blog/lmkit-made-embedding-storage-effortless/)** - Store and retrieve embeddings at any scale without external dependencies
- 🔗 **[Vector Database Connectors (Open Source)](https://github.com/LM-Kit/lm-kit-net-data-connectors)** - Integrate with Qdrant for semantic search and hybrid RAG pipelines
- 🧠 **[Semantic Kernel Integration (Open Source)](https://github.com/LM-Kit/lm-kit-net-semantic-kernel)** - Build intelligent workflows with Microsoft's Semantic Kernel + LM-Kit.NET
- 👁️ **[Vision Support](https://lm-kit.com/blog/lmkit-goes-multimodal/)** - Image understanding with vision language models
- 🎮 **Vulkan Backend** - Accelerated multi-GPU support for AMD, Intel, and NVIDIA
- ✨ **[Dynamic Sampling](https://lm-kit.com/blog/introducing-dynamic-sampling/)** - Up to 75% error reduction and 2x faster processing

👉 [See full changelog](https://docs.lm-kit.com/lm-kit-net/guides/changelog.html)

---

## Why LM-Kit.NET

**A complete AI stack with no moving parts.** LM-Kit.NET integrates inference, models, orchestration, and RAG into your .NET application as a single NuGet package. No Python runtimes, no containers, no external services. Everything runs in-process.

**Not every problem requires a massive LLM.** Dedicated task agents deliver faster execution, lower costs, and higher accuracy for specific workflows - with complete data control and minimal resource usage.

| Benefit | Description |
|---------|-------------|
| **Complete Data Sovereignty** | Sensitive information stays within your infrastructure |
| **Zero Network Latency** | Responses as fast as your hardware allows |
| **No Per-Token Costs** | Unlimited inference once deployed |
| **Offline Operation** | Works without internet connectivity |
| **Regulatory Compliance** | Meets GDPR, HIPAA, and data residency requirements by design |

---

## What You Can Build

- **Autonomous AI agents** that reason, plan, and execute multi-step tasks using your application's tools and APIs
- **RAG-powered knowledge assistants** over local documents, databases, and enterprise data sources
- **PDF chat and document Q&A** with retrieval, reranking, and grounded generation
- **Multi-agent workflows** that orchestrate specialized task agents for complex business processes
- **Voice-driven assistants** with speech-to-text, reasoning, and function calling
- **OCR and extraction pipelines** for invoices, forms, IDs, emails, and scanned documents
- **Compliance-focused text intelligence** - PII extraction, NER, classification, sentiment analysis

---

## Core Capabilities

### 🤖 AI Agents and Orchestration

Build autonomous AI agents that reason, plan, and execute complex workflows within your applications.

- **Task Agents** - Reusable specialists designed for specific tasks with high speed and accuracy
- **Agent Orchestration** - Compose multi-agent workflows with RAG, tools, and APIs under strict control
- **Function Calling** - Let models dynamically invoke your application's methods with structured parameters
- **Tool Registry** - Define and manage collections of tools agents can use
- **MCP Client Support** - Connect to Model Context Protocol servers for extended capabilities
- **Agent Memory** - Persistent memory that survives across conversation sessions
- **Reasoning Control** - Adjust reasoning depth for models that support extended thinking

### 🔍 Multimodal Intelligence

Process and understand content across text, images, documents, and audio.

- **Vision Language Models (VLM)** - Analyze images, extract information, answer questions about visual content
- **VLM-Based OCR** - High-accuracy text extraction from images and scanned content
- **Speech-to-Text** - Transcribe audio with voice activity detection and multi-language support
- **Document Processing** - Native support for PDF, DOCX, XLSX, PPTX, HTML, and image formats
- **Image Embeddings** - Generate semantic representations of images for similarity search
- **Image Segmentation** - Isolate subjects from backgrounds and segment image regions

### 📚 Retrieval-Augmented Generation (RAG)

Ground AI responses in your organization's knowledge with a flexible, extensible RAG framework.

- **Modular RAG Architecture** - Use built-in pipelines or implement custom retrieval strategies
- **Built-in Vector Database** - Store and search embeddings without external dependencies
- **PDF Chat and Document RAG** - Chat and retrieve over documents with dedicated workflows
- **Multimodal RAG** - Retrieve relevant content from both text and images
- **Advanced Chunking** - Markdown-aware, semantic, and layout-based chunking strategies
- **Reranking** - Improve retrieval precision with semantic reranking
- **External Vector Store Integration** - Connect to Qdrant and other vector databases

### 📊 Structured Data Extraction

Transform unstructured content into structured, actionable data.

- **Schema-Based Extraction** - Define extraction targets using JSON schemas or custom elements
- **Named Entity Recognition (NER)** - Extract people, organizations, locations, and custom entity types
- **PII Detection** - Identify and classify personal identifiers for privacy compliance
- **Multimodal Extraction** - Extract structured data from images and documents
- **Layout-Aware Processing** - Detect paragraphs and lines, support region-based workflows
- **Schema Discovery** - Automatically generate extraction schemas from sample documents

### 💡 Content Intelligence

Analyze and understand text and visual content.

- **Sentiment and Emotion Analysis** - Detect emotional tone from text and images
- **Custom Classification** - Categorize text and images into your defined classes
- **Keyword Extraction** - Identify key terms and phrases
- **Language Detection** - Identify languages from text, images, or audio
- **Summarization** - Condense long content with configurable strategies
- **Sarcasm Detection** - Recognize ironic or sarcastic nuances

### ✍️ Text Generation and Transformation

Generate and refine content with precise control.

- **Conversational AI** - Build context-aware chatbots with multi-turn memory
- **Constrained Generation** - Guide model outputs using JSON schemas, templates, or custom grammar rules
- **Translation** - Convert text between languages with confidence scoring
- **Text Enhancement** - Improve clarity, fix grammar, adapt tone

### 🛠️ Model Customization

Tailor models to your specific domain.

- **Fine-Tuning** - Train models on your data with LoRA support
- **Dynamic LoRA Loading** - Switch adapters at runtime without reloading base models
- **Quantization** - Optimize models for your deployment constraints
- **Training Dataset Tools** - Prepare and export datasets in standard formats (ShareGPT, etc.)

---

## Supported Models

LM-Kit.NET ships with domain-tuned models optimized for real-world tasks and maintains broad compatibility with models from leading providers. **New model architectures are added continuously** as they become available in the open-source community.

| Category | Models |
|----------|--------|
| **Text Models** | LLaMA, Mistral, Mixtral, Qwen, Phi, Gemma, Granite, DeepSeek, Falcon, GPT-OSS, SmolLM, and more |
| **Vision Models** | Qwen-VL, MiniCPM-V, Pixtral, Gemma Vision, LightOnOCR |
| **Embedding Models** | BGE, Nomic, Qwen Embedding, Gemma Embedding |
| **Speech Models** | Whisper (all sizes), with voice activity detection |

Browse production-ready models in the [Model Catalog](https://docs.lm-kit.com/lm-kit-net/guides/getting-started/model-catalog.html), or load models directly from any Hugging Face repository.

---

## Performance and Hardware

### The Fastest .NET Inference Engine

LM-Kit.NET automatically leverages the best available acceleration on any hardware. **Inference performance is continuously optimized** with each release through kernel improvements, memory management enhancements, and backend updates.

- **NVIDIA GPUs** - CUDA backends with optimized kernels (CUDA 12 and 13)
- **AMD/Intel GPUs** - Vulkan backend for cross-vendor GPU support
- **Apple Silicon** - Metal acceleration for M-series chips
- **Multi-GPU** - Distribute models across multiple GPUs
- **Hybrid Inference** - CPU+GPU inference for models exceeding VRAM capacity
- **CPU Fallback** - Optimized CPU inference with AVX/AVX2 support

### Dual Backend Architecture

Choose the optimal inference engine for your use case:
- **llama.cpp Backend** - Broad model compatibility, memory efficiency
- **ONNX Runtime** - Optimized inference for supported model formats

### Observability

- **OpenTelemetry Integration** - GenAI semantic conventions for distributed tracing and metrics
- **Inference Metrics** - Token counts, processing rates, generation speeds, context utilization, perplexity scores
- **Event Callbacks** - Fine-grained hooks for token sampling, tool invocations, and generation lifecycle

---

## Platform Support

### Operating Systems

| Platform | Requirements |
|----------|--------------|
| **Windows** | Windows 7 through Windows 11 |
| **macOS** | macOS 11+ (Intel and Apple Silicon) |
| **Linux** | glibc 2.27+ (x64 and ARM64) |

### .NET Frameworks

Compatible from **.NET Framework 4.6.2** through **.NET 10**, with optimized binaries for each version.

---

## Integration

### Zero Dependencies

LM-Kit.NET ships as a single NuGet package with absolutely no external dependencies:

```bash
dotnet add package LM-Kit.NET
```

No Python runtime. No containers. No external services. No native libraries to manage separately. The entire AI stack runs in-process within your .NET application.

### Ecosystem Connections

- **[Semantic Kernel](https://github.com/LM-Kit/lm-kit-net-semantic-kernel)** - Use LM-Kit.NET as a backend for Microsoft Semantic Kernel
- **[Vector Databases](https://github.com/LM-Kit/lm-kit-net-data-connectors)** - Integrate with Qdrant via open-source connectors
- **MCP Servers** - Connect to Model Context Protocol servers for extended tool access

---

## Getting Started

```csharp
using LMKit;
using LMKit.Model;

// Load a model
var model = new LM("path/to/model.gguf");

// Create a conversation
var conversation = new MultiTurnConversation(model);

// Chat
var response = await conversation.SubmitAsync("Explain quantum computing briefly.");
Console.WriteLine(response);
```

### Explore More

- 📖 [Documentation](https://docs.lm-kit.com)
- 💻 [GitHub Demo Repository](https://github.com/LM-Kit/lm-kit-net)
- 📦 [Model Catalog](https://docs.lm-kit.com/lm-kit-net/guides/getting-started/model-catalog.html)
- 📝 [Blog](https://lm-kit.com/blog/)

---

## Data Privacy and Security

Running inference locally provides inherent security advantages:

- **No data transmission** - Content never leaves your network
- **No third-party access** - No external services process your data
- **Audit-friendly** - Complete visibility into AI operations
- **Air-gapped deployment** - Works in disconnected environments

This architecture simplifies compliance with GDPR, HIPAA, SOC 2, and other regulatory frameworks.

---

© LM-Kit - All rights reserved.
