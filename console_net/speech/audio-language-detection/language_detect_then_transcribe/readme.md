# Audio Language Detection

Detect the language of an audio file with `SpeechToText.DetectLanguage` (no full transcription needed), then optionally transcribe with the detected language pinned for best quality.

## What it shows

- `SpeechToText.DetectLanguage(WaveFile)` returns `LanguageDetectionResult { Language, Confidence }`.
- `SpeechToText.GetSupportedLanguages()` lists the languages the loaded Whisper model can identify (model-dependent).
- `SpeechToText.Transcribe(audio, language)` accepts a pinned language. Pinning is faster and more accurate than running Whisper in `"auto"` mode.

## Run

```bash
cd console_net/speech/audio-language-detection/audio_language_detection
dotnet run -- "C:\calls\fr-call.mp3" "C:\calls\de-call.wav"
```

Pass any number of audio paths on the command line, or run with no args and enter them interactively.

## Where this fits

Routing calls, filing support tickets, picking the right RAG corpus, choosing the right translation pivot - every multilingual pipeline starts with "what language is this?". A 200 ms `DetectLanguage` call is the cheapest possible answer.
