using LMKit.Media.Audio;
using LMKit.Model;
using LMKit.Speech;
using NAudio.Wave;
using System.Diagnostics;
using System.Text;

namespace voice_activity_detection
{
    internal class Program
    {
        private static bool _isDownloading;

        static int Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Voice Activity Detection (VAD) Demo ===\n");

            string audioPath = args.Length >= 1 ? args[0] : PromptForPath();
            if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
            {
                Console.WriteLine("Audio file not found. Pass a path on the command line or provide one interactively.");
                return 1;
            }

            Console.WriteLine("Loading whisper-large-turbo3 ...");
            using LM model = LM.LoadFromModelID(
                "whisper-large-turbo3",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            using WaveFile audio = LoadAudio(audioPath);
            Console.WriteLine($"Audio loaded   : {audioPath}");
            Console.WriteLine($"Audio duration : {audio.Duration:mm\\:ss\\.ff}");
            Console.WriteLine();

            SpeechToText engine = new(model);

            RunPass(engine, audio, enableVad: false);
            RunPass(engine, audio, enableVad: true);

            Console.WriteLine();
            Console.WriteLine("VAD pass typically finishes faster on real-world audio with non-trivial silence,");
            Console.WriteLine("and never produces hallucinated text in silent regions.");
            return 0;
        }

        static void RunPass(SpeechToText engine, WaveFile audio, bool enableVad)
        {
            engine.EnableVoiceActivityDetection = enableVad;
            if (enableVad)
            {
                // Tuned defaults for a typical interview / meeting recording.
                engine.VadSettings.EnergyThreshold     = 0.45f;
                engine.VadSettings.MinSpeechDuration   = TimeSpan.FromMilliseconds(250);
                engine.VadSettings.MinSilenceDuration  = TimeSpan.FromMilliseconds(400);
                engine.VadSettings.SpeechPadding       = TimeSpan.FromMilliseconds(120);
            }

            string label = enableVad ? "VAD ON " : "VAD OFF";
            Console.WriteLine($"---- {label} ----");
            Stopwatch sw = Stopwatch.StartNew();
            SpeechToText.TranscriptionResult result = engine.Transcribe(audio);
            sw.Stop();

            Console.WriteLine($"  Wall time      : {sw.Elapsed:mm\\:ss\\.ff}");
            Console.WriteLine($"  RTF (audio / wall): {audio.Duration.TotalSeconds / sw.Elapsed.TotalSeconds:F2}x realtime");
            Console.WriteLine($"  Segments       : {result.Segments.Count}");
            Console.WriteLine($"  Transcript:");
            foreach (AudioSegment s in result.Segments)
            {
                Console.WriteLine($"    [{s.Start:mm\\:ss}-{s.End:mm\\:ss}] {s.Text.Trim()}");
            }
            Console.WriteLine();
        }

        static string PromptForPath()
        {
            Console.Write("Path to a WAV/MP3/M4A file (or drag-and-drop): ");
            return (Console.ReadLine() ?? "").Trim().Trim('"');
        }

        static WaveFile LoadAudio(string path)
        {
            if (WaveFile.IsValidWaveFile(path))
            {
                return new WaveFile(path);
            }

            using AudioFileReader reader = new(path);
            string tempPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");
            WaveFileWriter.CreateWaveFile16(tempPath, reader);
            try
            {
                return new WaveFile(File.ReadAllBytes(tempPath));
            }
            finally
            {
                File.Delete(tempPath);
            }
        }

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
