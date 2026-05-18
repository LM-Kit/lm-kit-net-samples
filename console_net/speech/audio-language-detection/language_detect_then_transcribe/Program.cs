using LMKit.Media.Audio;
using LMKit.Model;
using LMKit.Speech;
using NAudio.Wave;
using System.Text;

namespace audio_language_detection
{
    internal class Program
    {
        private static bool _isDownloading;

        static int Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Audio Language Detection Demo ===\n");

            List<string> paths = args.ToList();
            if (paths.Count == 0)
            {
                Console.WriteLine("Enter audio file path(s) one per line. Empty line to start.");
                while (true)
                {
                    Console.Write("> ");
                    string line = (Console.ReadLine() ?? "").Trim().Trim('"');
                    if (string.IsNullOrEmpty(line)) { break; }
                    paths.Add(line);
                }
            }

            if (paths.Count == 0)
            {
                Console.WriteLine("No audio provided. Exiting.");
                return 1;
            }

            Console.WriteLine();
            Console.WriteLine("Loading whisper-large-turbo3 ...");
            using LM model = LM.LoadFromModelID(
                "whisper-large-turbo3",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            SpeechToText engine = new(model);
            List<string> supported = engine.GetSupportedLanguages();
            Console.WriteLine($"This Whisper model can identify {supported.Count} languages.");
            Console.WriteLine();

            foreach (string p in paths)
            {
                if (!File.Exists(p))
                {
                    Console.WriteLine($"  [not found] {p}");
                    continue;
                }

                using WaveFile audio = LoadAudio(p);
                SpeechToText.LanguageDetectionResult lang = engine.DetectLanguage(audio);

                Console.WriteLine($"  {Path.GetFileName(p)}");
                Console.WriteLine($"    duration   : {audio.Duration:mm\\:ss\\.ff}");
                Console.WriteLine($"    language   : {lang.Language}");
                Console.WriteLine($"    confidence : {lang.Confidence:P1}");

                Console.Write($"    Transcribe with detected language pinned? (y/N) ");
                string ans = (Console.ReadLine() ?? "").Trim().ToLowerInvariant();
                if (ans == "y")
                {
                    SpeechToText.TranscriptionResult tr = engine.Transcribe(audio, lang.Language);
                    Console.WriteLine($"    transcript : {tr.Text.Trim()}");
                }
                Console.WriteLine();
            }

            return 0;
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
