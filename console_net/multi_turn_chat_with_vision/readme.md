# Multi-Turn Chat with Vision

A demo for multi-turn conversations about images using LM-Kit.NET vision-language models. Load an image and ask questions about its content.

## Features

- Chat about images with vision-language models
- Multi-turn conversation with context retention
- Support for multiple VLMs: MiniCPM, Qwen 3, Gemma 3, Ministral
- Real-time streaming responses
- Follow-up questions about the same image

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for the selected model (2.5–12 GB depending on model choice)

## Usage

1. Run the application
2. Select a vision-language model
3. Provide the path to an image file
4. The assistant describes the image automatically
5. Ask follow-up questions about the image content

## Commands

| Command | Description |
|---------|-------------|
| `/reset` | Clear history and load a new image |
| `/regenerate` | Generate a new response to your last input |
| `/continue` | Continue the last assistant response |

## Supported Image Formats

- PNG, JPG, JPEG, WebP, BMP, TIFF, and other common formats

## Example Questions

After loading an image, try asking:
- "What objects are in this image?"
- "What colors are prominent?"
- "Is there any text visible?"
- "Describe the mood of this scene"
- "What's happening in the background?"