# Audio Transcription

A demo for transcribing audio files to text using LM-Kit.NET with OpenAI Whisper models. Convert speech from audio recordings into accurate text transcriptions.

## Features

- Speech-to-text transcription using Whisper models
- Multiple model sizes: Tiny, Base, Small, Medium, Large, Large Turbo
- Support for WAV and other common audio formats (MP3, FLAC, etc.)
- Real-time segment output during transcription
- Multilingual speech recognition
- Performance metrics (processing time vs. audio duration)

## Prerequisites

- .NET 8.0 or later
- LM-Kit.NET SDK
- Minimal VRAM required (0.05–1.7 GB depending on model size)

## Usage

1. Run the application
2. Select a Whisper model (smaller = faster, larger = more accurate)
3. Enter the path to an audio file
4. View the transcription output in real-time

## Supported Audio Formats

- WAV (native support)
- MP3, FLAC, and other formats (automatically converted via NAudio)

## Model Selection Guide

| Model | VRAM | Best For |
|-------|------|----------|
| Tiny | ~50 MB | Quick drafts, low-resource devices |
| Base | ~80 MB | Balanced speed/accuracy |
| Small | ~260 MB | Good accuracy, moderate speed |
| Medium | ~820 MB | High accuracy |
| Large V3 | ~1.7 GB | Best accuracy |
| Large Turbo V3 | ~870 MB | Near-best accuracy, faster |