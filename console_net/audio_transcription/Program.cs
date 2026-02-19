using LMKit.Media.Audio;
using LMKit.Model;
using LMKit.Speech;
using NAudio.Wave;
using System.Diagnostics;
using System.Text;

namespace audio_transcription
{
    internal class Program
    {
        static bool _isDownloading;

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = Math.Round((double)bytesRead / contentLength.Value * 100, 2);
                Console.Write($"\rDownloading model {progressPercentage:0.00}%");
            }
            else
            {
                Console.Write($"\rDownloading model {bytesRead} bytes");
            }

            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.Clear();
                _isDownloading = false;
            }

            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");

            return true;
        }

        static void Main(string[] args)
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("Please select the speech to text model you want to use:\n");
            Console.WriteLine("0 - OpenAI Whisper Tiny (requires approximately 0.05 GB of VRAM)");
            Console.WriteLine("1 - OpenAI Whisper Base (requires approximately 0.08 GB of VRAM)");
            Console.WriteLine("2 - OpenAI Whisper Small (requires approximately 0.26 GB of VRAM)");
            Console.WriteLine("3 - OpenAI Whisper Medium (requires approximately 0.82 GB of VRAM)");
            Console.WriteLine("4 - OpenAI Whisper Large Turbo V3 (requires approximately 0.87 GB of VRAM)");
            Console.Write("Other: A custom model URI\n\n> ");

            string? input = Console.ReadLine();
            string? modelId = input?.Trim() switch
            {
                "0" => "whisper-tiny",
                "1" => "whisper-base",
                "2" => "whisper-small",
                "3" => "whisper-medium",
                "4" => "whisper-large-turbo3",
                _ => null
            };

            // Load model
            LM model;

            if (modelId != null)
            {
                model = LM.LoadFromModelID(
                    modelId,
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }
            else if (!string.IsNullOrWhiteSpace(input))
            {
                model = new LM(
                    new Uri(input.Trim('"')),
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }
            else
            {
                model = LM.LoadFromModelID(
                    "whisper-tiny",
                    downloadingProgress: OnDownloadProgress,
                    loadingProgress: OnLoadProgress);
            }

            Console.Clear();
            SpeechToText engine = new(model);

            engine.OnNewSegment += (sender, e) => Console.WriteLine(e.Segment.ToString());

            while (true)
            {
                Console.Write("Please enter the path to the audio file you want to transcribe (WAV format):\n\n> ");
                string? path = Console.ReadLine();

                if (string.IsNullOrEmpty(path))
                {
                    break;
                }

                try
                {
                    using var audio = LoadAudio(path);

                    Console.WriteLine();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("===== Transcription =====");
                    Console.ResetColor();
                    Console.WriteLine();

                    Stopwatch sw = Stopwatch.StartNew();
                    engine.Transcribe(audio);

                    Console.WriteLine();

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Transcription complete.");
                    Console.WriteLine($"  Done in {sw.Elapsed:mm\\:ss\\.ff} | Audio length: {audio.Duration:mm\\:ss\\.ff}");
                    Console.ResetColor();

                    Console.WriteLine();
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message} Please check the file path and permissions.");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("Demo ended. Press any key to exit.");
            _ = Console.ReadKey();
        }

        static WaveFile LoadAudio(string path)
        {
            path = path.Trim('"');
            if (!WaveFile.IsValidWaveFile(path))
            {
                using (var reader = new AudioFileReader(path))
                {
                    string tempFileName = $"{Guid.NewGuid():N}.wav";
                    string tempFilePath = Path.Combine(Path.GetTempPath(), tempFileName);

                    WaveFileWriter.CreateWaveFile16(tempFilePath, reader);
                    try
                    {
                        return new WaveFile(File.ReadAllBytes(tempFilePath));
                    }
                    finally
                    {
                        File.Delete(tempFilePath);
                    }
                }
            }
            else
            {
                return new WaveFile(path);
            }
        }
    }
}
