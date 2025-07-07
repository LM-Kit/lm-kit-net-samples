using LMKit.Media.Audio;
using LMKit.Model;
using LMKit.Speech;
using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace audio_transcription
{
    internal class Program
    {

        static bool _isDownloading;

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
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

        private static bool ModelLoadingProgress(float progress)
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
            Console.WriteLine("4 - OpenAI Whisper Large V3 (requires approximately 1.66 GB of VRAM)");
            Console.WriteLine("5 - OpenAI Whisper Large Turbo V3 (requires approximately 0.87 GB of VRAM)");

            Console.Write("Other entry: A custom model URI\n\n> ");

            string input = Console.ReadLine();
            string modelLink;

            switch (input.Trim())
            {
                case "0":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-tiny")
                        .ModelUri
                        .ToString();
                    break;
                case "1":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-base")
                        .ModelUri
                        .ToString();
                    break;
                case "2":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-small")
                        .ModelUri
                        .ToString();
                    break;
                case "3":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-medium")
                        .ModelUri
                        .ToString();
                    break;
                case "4":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-large3")
                        .ModelUri
                        .ToString();
                    break;
                case "5":
                    modelLink = ModelCard
                        .GetPredefinedModelCardByModelID("whisper-large-turbo3")
                        .ModelUri
                        .ToString();
                    break;
                default:
                    modelLink = input.Trim().Trim('"');
                    break;
            }

            //Loading model
            Uri modelUri = new Uri(modelLink);
            LM model = new LM(
                modelUri,
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);

            Console.Clear();
            SpeechToText engine = new SpeechToText(model);

            engine.OnNewSegment += (sender, e) => Console.WriteLine(e.Segment.ToString());

            while (true)
            {
                Console.Write("Please enter the path to the audio file you want to transcribe (WAV format):\n\n> ");
                string path = Console.ReadLine();

                if (string.IsNullOrEmpty(path))
                {
                    break;
                }

                try
                {
                    using (var audio = LoadAudio(path))
                    {
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("===== Transcription =====");
                        Console.ResetColor();
                        Console.WriteLine();

                        Stopwatch sw = Stopwatch.StartNew();
                        engine.Transcribe(audio);

                        // Add a blank line for spacing
                        Console.WriteLine();

                        // Highlight completion with a green banner
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("┌────────────────── Transcription Complete ──────────────────┐");
                        Console.WriteLine($"│   ✅ Done in {sw.Elapsed:mm\\:ss\\.ff}        🔊 Audio length: {audio.Duration:mm\\:ss\\.ff}     │");
                        Console.WriteLine("└────────────────────────────────────────────────────────────┘");
                        Console.ResetColor();

                        // Extra spacing after the banner
                        Console.WriteLine();
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Error: Unable to open the file at '{path}'. Details: {e.Message} Please check the file path and permissions.");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("The program ended. Press any key to exit the application.");
            _ = Console.ReadKey();
        }


        static WaveFile LoadAudio(string path)
        {
            path = path.Trim('"');
            if (!WaveFile.IsValidWaveFile(path))
            {//converting to WAV using NAudio lib
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