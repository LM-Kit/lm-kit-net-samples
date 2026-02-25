using LMKit.Agents;
using LMKit.Agents.Orchestration;
using LMKit.Media.Audio;
using LMKit.Model;
using LMKit.Speech;
using NAudio.Wave;
using System.Diagnostics;
using System.Text;

namespace smart_meeting_assistant
{
    internal class Program
    {
        static bool _isDownloading;

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            else
                Console.Write($"\rDownloading model {bytesRead} bytes");
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.Clear(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }

        static LM LoadChatModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "qwen3:8b",
                "1" => "gemma3:12b",
                "2" => "qwen3:14b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.5:27b",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "qwen3:8b";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static LM LoadWhisperModel(string input)
        {
            string? modelId = input?.Trim() switch
            {
                "0" => "whisper-tiny",
                "1" => "whisper-base",
                "2" => "whisper-small",
                "3" => "whisper-medium",
                "4" => "whisper-large-turbo3",
                _ => null
            };

            if (modelId != null)
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            string uri = !string.IsNullOrWhiteSpace(input) ? input.Trim('"') : "whisper-base";
            if (!uri.Contains("://"))
                return LM.LoadFromModelID(uri, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            return new LM(new Uri(uri), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        private static async Task Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            Console.WriteLine("=== Smart Meeting Assistant Demo ===\n");
            Console.WriteLine("This demo transcribes meeting audio, then uses a multi-agent pipeline to produce:");
            Console.WriteLine("  - Executive summary");
            Console.WriteLine("  - Action items with owners and deadlines");
            Console.WriteLine("  - Follow-up email draft\n");

            // Step 1: Select Whisper model
            Console.WriteLine("Step 1: Select a speech-to-text model:\n");
            Console.WriteLine("0 - OpenAI Whisper Tiny    (~0.05 GB VRAM)");
            Console.WriteLine("1 - OpenAI Whisper Base    (~0.08 GB VRAM) [Recommended]");
            Console.WriteLine("2 - OpenAI Whisper Small   (~0.26 GB VRAM)");
            Console.WriteLine("3 - OpenAI Whisper Medium   (~0.82 GB VRAM)");
            Console.WriteLine("4 - OpenAI Whisper Large Turbo V3 (~0.87 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? whisperInput = Console.ReadLine();
            LM whisperModel = LoadWhisperModel(whisperInput ?? "");

            Console.Clear();

            // Step 2: Select chat model
            Console.WriteLine("Step 2: Select a chat model for analysis:\n");
            Console.WriteLine("0 - Alibaba Qwen-3 8B      (~6 GB VRAM) [Recommended]");
            Console.WriteLine("1 - Google Gemma 3 12B      (~9 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen-3 14B      (~10 GB VRAM)");
            Console.WriteLine("3 - Microsoft Phi-4 14.7B    (~11 GB VRAM)");
            Console.WriteLine("4 - OpenAI GPT OSS 20B       (~16 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM 4.7 Flash 30B   (~18 GB VRAM)");
            Console.WriteLine("6 - Alibaba Qwen-3.5 27B     (~18 GB VRAM)");
            Console.Write("Other: Custom model URI or model ID\n\n> ");

            string? chatInput = Console.ReadLine();
            LM chatModel = LoadChatModel(chatInput ?? "");

            Console.Clear();
            Console.WriteLine("=== Smart Meeting Assistant ===\n");

            // Create speech-to-text engine
            SpeechToText sttEngine = new(whisperModel);

            // Create the pipeline agents
            var summarizerAgent = Agent.CreateBuilder(chatModel)
                .WithPersona(@"Meeting Summarizer - You are an expert meeting summarizer. Your job is to read a meeting
transcript and produce a clear, concise executive summary.

Your summary should include:
- Meeting context (what was discussed, who participated if mentioned)
- Key decisions made
- Important discussion points
- 3-7 bullet points covering the most critical takeaways

Be concise and factual. Do not invent details not present in the transcript.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var actionExtractorAgent = Agent.CreateBuilder(chatModel)
                .WithPersona(@"Action Item Extractor - You are an expert at identifying action items from meeting content.

From the meeting summary and context provided, extract all action items in this exact format:

ACTION ITEMS:
1. [Task description] | Owner: [Person name or 'Unassigned'] | Deadline: [Date or 'TBD'] | Priority: [High/Medium/Low]
2. ...

Rules:
- Only extract items that are clearly assigned or agreed upon
- If no owner is mentioned, mark as 'Unassigned'
- If no deadline is mentioned, mark as 'TBD'
- Infer priority from context and urgency cues
- Include a count at the end: 'Total: X action items'")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            var emailDrafterAgent = Agent.CreateBuilder(chatModel)
                .WithPersona(@"Follow-Up Email Drafter - You are a professional email writer. Your job is to draft
a follow-up email based on the meeting summary and action items.

The email should:
- Have a clear subject line starting with 'Meeting Follow-Up:'
- Open with a brief thank you for attending
- Include the executive summary in 2-3 sentences
- List all action items with owners and deadlines
- End with next steps or next meeting date if mentioned
- Use a professional but friendly tone
- Be ready to send (not a template)

Format the email with Subject:, To:, and Body: sections.")
                .WithPlanning(PlanningStrategy.None)
                .Build();

            // Create the pipeline
            var pipeline = new PipelineOrchestrator()
                .AddStage("Summarizer", summarizerAgent)
                .AddStage("ActionExtractor", actionExtractorAgent)
                .AddStage("EmailDrafter", emailDrafterAgent);

            Console.WriteLine("Pipeline Stages:");
            Console.WriteLine("  1. Transcribe  → Convert audio to text (Whisper)");
            Console.WriteLine("  2. Summarizer  → Create executive summary");
            Console.WriteLine("  3. Extractor   → Identify action items");
            Console.WriteLine("  4. Email Draft → Compose follow-up email\n");
            Console.WriteLine("Enter the path to a meeting audio file (WAV, MP3, FLAC, etc.).");
            Console.WriteLine("Type 'quit' to exit.\n");

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("Audio file: ");
                Console.ResetColor();

                string? path = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("Please enter a file path.\n");
                    continue;
                }

                if (path.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    break;

                Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║              SMART MEETING ASSISTANT                          ║");
                Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");

                try
                {
                    Stopwatch totalSw = Stopwatch.StartNew();

                    // Stage 1: Transcription
                    Console.WriteLine("┌─── Stage 1: TRANSCRIPTION ─────────────────────────────────────");
                    Console.ForegroundColor = ConsoleColor.Yellow;

                    using var audio = LoadAudio(path.Trim('"'));
                    var transcriptBuilder = new StringBuilder();

                    sttEngine.OnNewSegment += SegmentHandler;
                    void SegmentHandler(object? sender, SpeechToText.NewSegmentEventArgs e)
                    {
                        string text = e.Segment.ToString();
                        transcriptBuilder.AppendLine(text);
                        Console.WriteLine($"  {text}");
                    }

                    Stopwatch sttSw = Stopwatch.StartNew();
                    sttEngine.Transcribe(audio);
                    sttEngine.OnNewSegment -= SegmentHandler;
                    sttSw.Stop();

                    string transcript = transcriptBuilder.ToString().Trim();
                    Console.ResetColor();

                    if (string.IsNullOrWhiteSpace(transcript))
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("No speech detected in the audio file.");
                        Console.ResetColor();
                        Console.WriteLine("└─────────────────────────────────────────────────────────────────\n");
                        continue;
                    }

                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"  (Transcription: {sttSw.Elapsed:mm\\:ss\\.ff} | Audio: {audio.Duration:mm\\:ss\\.ff})");
                    Console.ResetColor();
                    Console.WriteLine("└─────────────────────────────────────────────────────────────────\n");

                    // Stages 2-4: Pipeline
                    var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
                    string[] stageNames = { "SUMMARIZER", "ACTION EXTRACTOR", "EMAIL DRAFTER" };
                    ConsoleColor[] stageColors = { ConsoleColor.Green, ConsoleColor.Magenta, ConsoleColor.Cyan };

                    var result = await pipeline.ExecuteAsync(
                        $"Here is the full transcript of a meeting:\n\n{transcript}\n\nPlease process this meeting transcript.",
                        cts.Token);

                    int stageNumber = 0;
                    foreach (var stageResult in result.AgentResults)
                    {
                        if (stageNumber < stageNames.Length)
                        {
                            Console.WriteLine($"┌─── Stage {stageNumber + 2}: {stageNames[stageNumber]} ───────────────────────────────────────");
                            Console.ForegroundColor = stageColors[stageNumber];
                        }

                        if (stageResult.IsSuccess)
                        {
                            Console.WriteLine(stageResult.Content ?? "");
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Stage failed: {stageResult.Error?.Message ?? "Unknown error"}");
                        }

                        Console.ResetColor();
                        Console.WriteLine($"└─────────────────────────────────────────────────────────────────\n");
                        stageNumber++;
                    }

                    totalSw.Stop();
                    Console.WriteLine("--- Meeting Processing Complete ---");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"(Stages completed: {result.AgentResults.Count + 1} | Duration: {totalSw.Elapsed.TotalSeconds:F1}s)");
                    Console.ResetColor();
                    Console.WriteLine();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("\nProcessing timed out.");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\nError: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine("\nDemo ended. Press any key to exit.");
            Console.ReadKey();
        }

        static WaveFile LoadAudio(string path)
        {
            if (!WaveFile.IsValidWaveFile(path))
            {
                using var reader = new AudioFileReader(path);
                string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.wav");

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
            else
            {
                return new WaveFile(path);
            }
        }
    }
}
