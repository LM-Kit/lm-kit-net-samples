# AI Resume Parser

A demo that extracts structured candidate profiles from resumes using **LM-Kit.NET's TextExtraction API** with **vision-language models**. Feed it any resume (text, PDF, DOCX, or scanned image) and get back structured data: contact info, work experience, education, skills, certifications, and languages.

## Features

- **Vision-Language Models**: Uses VLM models to process both text and image-based resumes
- **Structured Profile Extraction**: Name, email, phone, location, summary, experience, education, skills, certifications, languages
- **Nested Data**: Work experience and education extracted as structured arrays with individual fields
- **Multiple Formats**: Parse `.txt` files directly, `.pdf`/`.docx` via document attachment, or scanned images (`.png`, `.jpg`, `.bmp`, `.tiff`)
- **Built-In Sample**: Type `sample` to instantly see extraction in action with a realistic resume
- **JSON Output**: Get machine-readable JSON alongside the formatted display
- **Fully Local**: All processing on your hardware, candidate data never leaves your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5 to 18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema of `TextExtractionElement` definitions and a vision-language model. The VLM can process both text documents and scanned resume images, extracting structured fields from any format.

1. Select a vision-language model from the menu
2. Define extraction schema (name, experience, education, skills, etc.)
3. Feed resume content (text string, document attachment, or image)
4. Call `Parse()` to extract structured data
5. Display results and JSON output

## Usage

1. Run the application
2. Select a VLM model from the menu
3. Type `sample` for the built-in resume or enter a file path
4. View the extracted candidate profile and JSON output
5. Type `q` to quit

## Example Output

```
╔═══════════════════════════════════════════════════════════════╗
║                    CANDIDATE PROFILE                          ║
╚═══════════════════════════════════════════════════════════════╝

  Full Name: Maria Rodriguez
  Email: maria.rodriguez@email.com
  Phone: (415) 555-0192
  Location: San Francisco, CA
  Professional Summary: Senior Full-Stack Engineer with 8+ years...
  Work Experience: [3 entries with company, title, period, achievements]
  Education: [2 entries with institution, degree, year]
  Skills: Python, TypeScript, Go, Java, SQL, React, Next.js, ...
  Certifications: AWS Solutions Architect Professional, CKA, ...
  Languages: English (Native), Spanish (Native), ...
```

## Models

| Option | Model | Approx. VRAM |
|--------|-------|-------------|
| 0 | Z.ai GLM-V 4.6 Flash 10B | ~7 GB |
| 1 | MiniCPM o 4.5 9B | ~5.9 GB |
| 2 | Alibaba Qwen 3.5 2B | ~2 GB |
| 3 | Alibaba Qwen 3.5 4B | ~3.5 GB |
| 4 | Alibaba Qwen 3.5 9B (Recommended) | ~7 GB |
| 5 | Google Gemma 3 4B | ~5.7 GB |
| 6 | Google Gemma 3 12B | ~11 GB |
| 7 | Alibaba Qwen 3.5 27B | ~18 GB |
| 8 | Mistral Ministral 3 8B | ~6.5 GB |
