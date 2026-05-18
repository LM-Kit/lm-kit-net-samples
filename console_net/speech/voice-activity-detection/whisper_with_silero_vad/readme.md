# Voice Activity Detection (VAD)

Compare Whisper transcription with VAD on vs off. The same audio is transcribed twice and the speedup, segment count, and hallucination delta are printed side by side.

## What it shows

- `SpeechToText.EnableVoiceActivityDetection` toggles a built-in Silero VAD frontend.
- `SpeechToText.VadSettings.{EnergyThreshold, MinSpeechDuration, MinSilenceDuration, SpeechPadding, MaxSpeechDuration}` tune the gate behaviour.
- VAD-gated transcription is faster on real-world audio (no work spent on silence) and never produces hallucinated text in silent regions.

## Run

```bash
cd console_net/speech/voice-activity-detection/voice_activity_detection
dotnet run -- "C:\path\to\interview.mp3"
```

The first argument is the audio path. WAV is consumed directly; anything NAudio understands (MP3, M4A, OGG, FLAC) is transcoded to a temporary 16 kHz WAV at load time.

## Where this fits

For long-form recordings (calls, podcasts, lectures, meeting rooms), VAD typically cuts transcription wall-time by 30 to 60 percent and eliminates the "Whisper invented dialogue during the silence" failure mode that breaks production transcription pipelines.
