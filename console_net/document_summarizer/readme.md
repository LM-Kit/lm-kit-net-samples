# Document Summarizer

A demo for summarizing PDF files, images, and documents using LM-Kit.NET vision-language models. Generate titles and concise summaries automatically.

## Features

- Summarize PDF documents, images, and text files
- Automatic title generation
- Configurable summary length
- Support for multiple VLMs: MiniCPM, Qwen 3, Qwen 3.5, Gemma 3, Ministral
- Custom guidance for summary style or language

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5~18 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Enter the path to a document
4. View the generated title and summary

## Supported Formats

- Documents: PDF, DOCX, XLSX, PPTX, EML, MBOX
- Images: PNG, JPG, TIFF, BMP, WebP
- Text: TXT, HTML

## Configuration

Customize the summarizer behavior:

```csharp
Summarizer summarizer = new(model)
{
    GenerateTitle = true,
    GenerateContent = true,
    MaxContentWords = 100,
    Guidance = "Always summarize in French"
};
```

## Example Output

```
Title: Quarterly Financial Report Q3 2024
Summary: The company reported strong revenue growth of 15% year-over-year, 
driven by expansion in the APAC region. Operating margins improved to 22%, 
while R&D investments increased by 8% to support new product development.
```