# Chat with PDF

A demo for conversational Q&A over PDF documents using LM-Kit.NET. Load one or more PDF files and ask questions in natural language.

## Features

- Chat with PDF documents using local vision-language models
- Support for multiple VLMs: MiniCPM, Qwen 3, Qwen 3.5, Gemma 3, Ministral
- Two processing modes:
  - **Standard extraction**: Fast text-based processing with optional OCR fallback
  - **Vision-based understanding**: Better accuracy for complex layouts and scanned documents
- RAG pipeline with semantic passage retrieval
- Multi-document support with automatic context management
- Local caching for faster re-indexing of previously processed documents

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5~18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Choose a processing mode (standard or vision-based)
4. Load one or more PDF documents
5. Ask questions about your documents

## Commands

| Command | Description |
|---------|-------------|
| `/help` | Show available commands |
| `/status` | Display loaded documents and configuration |
| `/add` | Add more PDF documents |
| `/restart` | Clear chat history, keep documents |
| `/reset` | Remove all documents and start fresh |
| `/regenerate` | Generate a new response to your last question |