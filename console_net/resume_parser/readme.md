# AI Resume Parser

A demo that extracts structured candidate profiles from resumes using **LM-Kit.NET's TextExtraction API**. Feed it any resume and get back structured data: contact info, work experience, education, skills, certifications, and languages.

## Features

- **Structured Profile Extraction**: Name, email, phone, location, summary, experience, education, skills, certifications, languages
- **Nested Data**: Work experience and education extracted as structured arrays with individual fields
- **Multiple Formats**: Parse `.txt` files directly or `.pdf`/`.docx` via document attachment
- **Built-In Sample**: Type `sample` to instantly see extraction in action with a realistic resume
- **JSON Output**: Get machine-readable JSON alongside the formatted display
- **Fully Local**: All processing on your hardware, candidate data never leaves your machine

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (6-18 GB depending on model choice)

## How It Works

The demo uses LM-Kit.NET's `TextExtraction` class with a schema of `TextExtractionElement` definitions. The extraction engine parses the resume content and populates each field automatically.

1. Define extraction schema (name, experience, education, skills, etc.)
2. Feed resume content (text string or document attachment)
3. Call `Parse()` to extract structured data
4. Display results and JSON output

## Usage

1. Run the application
2. Select a model from the menu
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
| 0 | Qwen-3 8B (Recommended) | ~6 GB |
| 1 | Gemma 3 12B | ~9 GB |
| 2 | Qwen-3 14B | ~10 GB |
| 3 | Phi-4 14.7B | ~11 GB |
| 4 | GPT OSS 20B | ~16 GB |
| 5 | GLM 4.7 Flash 30B | ~18 GB |
| 6 | Qwen-3.5 27B | ~18 GB |
