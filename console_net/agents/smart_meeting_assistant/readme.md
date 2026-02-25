# Smart Meeting Assistant

A demo showcasing an end-to-end meeting processing pipeline that combines **speech-to-text transcription** with **multi-agent orchestration**. The pipeline transcribes meeting audio locally, then produces an executive summary, structured action items, and a ready-to-send follow-up email.

## Features

- **Whisper Transcription**: Local speech-to-text with real-time segment display
- **PipelineOrchestrator**: Three-stage agent pipeline (Summarizer, Extractor, Email Drafter)
- **Action Item Extraction**: Structured output with owners, deadlines, and priorities
- **Follow-Up Email Draft**: Professional email ready to review and send
- **Multi-Model Architecture**: Separate Whisper model for speech and chat model for analysis
- **Multi-Format Audio**: Supports WAV, MP3, FLAC, and other audio formats
- **Fully Local**: All processing runs on your hardware with no cloud dependency

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Sufficient VRAM for both models (Whisper: 0.05-0.87 GB + Chat: 6-18 GB)

## How It Works

The pipeline chains two AI capabilities:

1. **Transcribe**: Whisper model converts audio to text with real-time segment output
2. **Summarize**: Agent creates a concise executive summary of the meeting
3. **Extract**: Agent identifies action items with owners, deadlines, and priorities
4. **Draft**: Agent composes a professional follow-up email with all key details

Each stage builds on the previous stage's output, producing increasingly refined and actionable results.

## Usage

1. Run the application
2. Select a Whisper model (speech-to-text)
3. Select a chat model (analysis and generation)
4. Enter the path to a meeting audio file
5. Watch real-time transcription followed by multi-stage analysis
6. Receive summary, action items, and a draft email

## Example Output

```
Stage 1: TRANSCRIPTION
  [00:00 - 00:05] Good morning everyone, let's start the sprint review...
  [00:05 - 00:12] Sarah, can you update us on the API redesign?
  ...

Stage 2: SUMMARIZER
  Executive Summary:
  - Sprint review covering API redesign and Q1 planning
  - Decision to adopt REST-first approach with GraphQL layer
  ...

Stage 3: ACTION EXTRACTOR
  ACTION ITEMS:
  1. Complete API spec draft | Owner: Sarah | Deadline: Feb 7 | Priority: High
  2. Set up staging environment | Owner: Mike | Deadline: Feb 10 | Priority: Medium
  ...

Stage 4: EMAIL DRAFTER
  Subject: Meeting Follow-Up: Sprint Review - API Redesign & Q1 Planning
  ...
```

## Models

### Whisper Models (Speech-to-Text)

| Option | Model | Approx. VRAM |
|--------|-------|-------------|
| 0 | Whisper Tiny | ~0.05 GB |
| 1 | Whisper Base (Recommended) | ~0.08 GB |
| 2 | Whisper Small | ~0.26 GB |
| 3 | Whisper Medium | ~0.82 GB |
| 4 | Whisper Large Turbo V3 | ~0.87 GB |

### Chat Models (Analysis)

| Option | Model | Approx. VRAM |
|--------|-------|-------------|
| 0 | Qwen-3 8B (Recommended) | ~6 GB |
| 1 | Gemma 3 12B | ~9 GB |
| 2 | Qwen-3 14B | ~10 GB |
| 3 | Phi-4 14.7B | ~11 GB |
| 4 | GPT OSS 20B | ~16 GB |
| 5 | GLM 4.7 Flash 30B | ~18 GB |
| 6 | Qwen-3.5 27B | ~18 GB |

## Configuration

- **Transcription timeout**: Depends on audio length
- **Pipeline timeout**: 10 minutes per processing run
- **Audio formats**: WAV (native), MP3/FLAC/others (auto-converted via NAudio)
