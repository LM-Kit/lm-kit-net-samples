# Document Processing Agent

An AI agent equipped with 11 built-in Document tools for processing PDFs and images through natural language instructions.

## Features

- **PDF inspection**: get page count, dimensions, metadata, and text content
- **PDF splitting**: extract page ranges into separate files
- **PDF merging**: combine multiple PDF files into one
- **PDF rendering**: convert pages to JPEG, PNG, or BMP images with configurable zoom and quality
- **Image to PDF**: combine images (JPEG, PNG, BMP) into a single PDF document
- **PDF unlocking**: remove password protection from PDFs using the known password
- **Image deskewing**: correct rotation in scanned documents
- **Image cropping**: auto-remove uniform borders from scans
- **Image resizing**: scale images or fit within bounding boxes preserving aspect ratio
- **Text extraction**: extract text from PDF, DOCX, XLSX, PPTX, and HTML files
- **OCR**: extract text from images using Tesseract (34 languages)
- **Tool call monitoring**: see which tools the agent uses in real time
- **Interactive console**: continuous conversation loop for multi-step workflows

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- A tool-calling capable model (~6.5 GB VRAM for Qwen 3 8B recommended)

## How It Works

The agent uses LM-Kit.NET's tool-calling architecture to:
1. **Parse** your natural language instruction into one or more tool calls
2. **Select** the appropriate Document tools (PDF, image, text, OCR)
3. **Execute** tools and interpret results
4. **Chain** operations when a single prompt requires multiple steps (e.g., "deskew, crop, then OCR")
5. **Report** what was done, with file paths and extracted content

## Tools Included

| Tool | Description |
|------|-------------|
| `pdf_info` | Get page count, dimensions, metadata, and text content from PDFs |
| `pdf_split` | Extract page ranges into separate PDF files |
| `pdf_merge` | Combine multiple PDF files into one |
| `pdf_to_image` | Convert PDF pages to JPEG, PNG, or BMP images with configurable zoom and quality |
| `image_to_pdf` | Convert one or more images (JPEG, PNG, BMP) into a single PDF |
| `pdf_unlock` | Remove password protection from a PDF using the known password |
| `image_deskew` | Correct rotation in scanned document images |
| `image_crop` | Auto-remove uniform borders from scans |
| `image_resize` | Scale images or fit within bounding boxes preserving aspect ratio |
| `document_text` | Extract text from PDF, DOCX, XLSX, PPTX, and HTML files |
| `ocr` | Extract text from images using Tesseract (34 languages) |

## Agent Configuration

```csharp
var agent = Agent.CreateBuilder(model)
    .WithPersona("Document Processing Assistant")
    .WithInstruction("You are a document processing assistant with access to tools for handling PDFs and images.")
    .WithTools(tools =>
    {
        tools.Register(BuiltInTools.PdfInfo);
        tools.Register(BuiltInTools.PdfSplit);
        tools.Register(BuiltInTools.PdfMerge);
        tools.Register(BuiltInTools.PdfToImage);
        tools.Register(BuiltInTools.ImageToPdf);
        tools.Register(BuiltInTools.PdfUnlock);
        tools.Register(BuiltInTools.ImageDeskew);
        tools.Register(BuiltInTools.ImageCrop);
        tools.Register(BuiltInTools.ImageResize);
        tools.Register(BuiltInTools.DocumentText);
        tools.Register(BuiltInTools.Ocr);
    })
    .WithMaxIterations(15)
    .Build();
```

## Usage

1. Run the application
2. Select a tool-calling model from the menu (Qwen 3 8B recommended)
3. Type document processing tasks in natural language
4. Watch the agent call tools and produce results
5. Type 'q' to quit

## Example Prompts

```
You — How many pages does 'contract.pdf' have?

You — Extract pages 1-3 from 'report.pdf' into 'summary.pdf'

You — Merge 'part1.pdf' and 'part2.pdf' into 'combined.pdf'

You — Deskew 'scan.png', crop its borders, then resize to 1200x1600

You — Run OCR on 'receipt.jpg' in French

You — Render page 5 of 'manual.pdf' as a PNG at 2x zoom

You — Extract text from 'quarterly_report.docx'
```

## Example Output

```
LM-Kit Document Processing Agent
An AI agent with Document tools for PDF processing, image preprocessing, text extraction, and OCR.
Type a document processing task, or 'q' to quit.

You — Extract pages 1-3 from 'report.pdf' into 'summary.pdf', then get the text from page 1

Processing...
  │ Tool: pdf_split({"operation":"split","inputPath":"report.pdf","outputPath":"summary.pdf","pageRa...)
  │ Tool: document_text({"operation":"extract","filePath":"report.pdf","pageRange":"1"})

Assistant — Done. I extracted pages 1-3 from 'report.pdf' into 'summary.pdf' (3 pages).

The text from page 1 reads:
"Quarterly Financial Report — Q4 2025..."
  [2 tool call(s), 4.2s, 3 inference(s)]

You — Now deskew 'scan_001.png' and run OCR on it

Processing...
  │ Tool: image_deskew({"operation":"deskew","inputPath":"scan_001.png","outputPath":"scan_001_deske...)
  │ Tool: ocr({"operation":"recognize","imagePath":"scan_001_deskewed.png","language":"eng"})

Assistant — I deskewed 'scan_001.png' (corrected 1.8° rotation) and saved the result as 'scan_001_deskewed.png'. OCR extracted the following text:

"Invoice #2024-0847
Date: November 15, 2025
Amount Due: $1,234.56"
  [2 tool call(s), 6.1s, 3 inference(s)]
```

## Understanding the Output

The demo displays different types of output in different colors:
- **Gray**: Tool invocations (tool name and arguments)
- **Blue**: Internal reasoning
- **White**: Final response visible to the user

## Key Classes

- **`Agent`** (`LMKit.Agents`): the AI agent that orchestrates tool calling based on natural language.
- **`BuiltInTools`** (`LMKit.Agents.Tools.BuiltIn`): factory class providing access to the Document tool category.
- **`AgentExecutor`** (`LMKit.Agents`): executes the agent with streaming output via `AfterTextCompletion`.
- **`AgentExecutionResult`**: result object with `Content`, `IsSuccess`, `ToolCalls`, and `Duration`.

## Customization

Adjust the agent's behavior by modifying:
- `MaxIterations`: increase for complex multi-step document workflows (default: 15)
- Add `BuiltInTools.FileSystem` to let the agent browse directories and batch-process files
- Add `BuiltInTools.Json` or `BuiltInTools.Csv` to parse extracted text into structured data
- Combine with `DocumentSplitting` for AI-vision boundary detection before physical splitting
- Modify the agent persona and instruction for domain-specific document workflows
