# Text Rewriter

A demo for rewriting text in different communication styles using LM-Kit.NET. Transform your content to be more concise, professional, or friendly.

## Features

- Rewrite text in multiple communication styles
- Support for multiple LLMs: Ministral, Llama, Gemma, Phi, Qwen, Granite, GPT OSS, GLM
- Real-time streaming output
- Compare all styles at once
- Multilingual support

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (3–18 GB depending on model choice)

## Usage

1. Run the application
2. Select a language model
3. Enter text to rewrite
4. Choose a communication style (or compare all styles)
5. View the rewritten output

## Communication Styles

| Style | Description |
|-------|-------------|
| Concise | Short and to the point |
| Professional | Formal business tone |
| Friendly | Casual and approachable |

## Example

**Original:**
> I wanted to reach out regarding the project timeline. We might need to push back the deadline a bit because some things came up.

**Concise:**
> Project deadline may need extension due to unforeseen issues.

**Professional:**
> I am writing to inform you that the project timeline requires adjustment. Due to recent developments, we may need to extend the deadline.

**Friendly:**
> Hey! Just a quick heads up—we might need a little more time on the project. A few things popped up, but we're on it!