# Text Corrector

A demo for correcting grammar, spelling, and punctuation errors in text using LM-Kit.NET language models.

## Features

- Fix grammar mistakes and typos
- Correct punctuation and capitalization
- Preserve original meaning while improving clarity
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Real-time streaming output

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–16 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter text with errors
4. View the corrected output

## Example Corrections

| Input | Output |
|-------|--------|
| "Their going to the store" | "They're going to the store" |
| "I could of done better" | "I could have done better" |
| "Me and him went out" | "He and I went out" |
| "Its a nice day" | "It's a nice day" |
| "The team are winning" | "The team is winning" |

## Common Mistakes Handled

- Subject-verb agreement
- Their/there/they're confusion
- Its/it's confusion
- Your/you're confusion
- Could of ? could have
- Affect vs. effect
- Run-on sentences
- Missing punctuation
- Capitalization errors