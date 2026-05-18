using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace sarcasm_detection
{
    internal sealed record TriageRow(
        int Id, DateTime ClassifiedAt, bool IsSarcastic, double Confidence,
        long LatencyMs, string Text);

    internal class Program
    {
        static bool _isDownloading;

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            WriteHeader();

            Console.WriteLine("Loading lmkit-sarcasm-detection (fine-tuned English classifier) ...");
            using LM model = LM.LoadFromModelID(
                "lmkit-sarcasm-detection",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            SarcasmDetection classifier = new(model);

            Console.Clear();
            WriteHeader();
            PrintMenu();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("> ");
                Console.ResetColor();
                string? choice = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(choice)) { continue; }

                switch (choice.ToLowerInvariant())
                {
                    case "1": case "live":
                        RunLiveMode(classifier);
                        break;

                    case "2": case "sample":
                        RunBatchMode(classifier, "Built-in sample (12 messages)", BuiltInSample(), promptCsv: true);
                        break;

                    case "3": case "file":
                        RunFileMode(classifier);
                        break;

                    case "q": case "quit": case "exit":
                        return;

                    case "?": case "help": case "menu":
                        PrintMenu();
                        break;

                    default:
                        Console.WriteLine("Unknown choice. Type '?' to see the menu.");
                        break;
                }
            }
        }

        static void RunLiveMode(SarcasmDetection classifier)
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Type one message per line. Empty line returns to menu.");
            Console.WriteLine();

            int idx = 0;
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("message > ");
                Console.ResetColor();
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine(); return; }

                idx++;
                Stopwatch sw = Stopwatch.StartNew();
                bool isSarc = classifier.IsSarcastic(text);
                sw.Stop();

                PrintResult(idx, text, isSarc, classifier.Confidence, sw.ElapsedMilliseconds);
                Console.WriteLine();
            }
        }

        static void RunFileMode(SarcasmDetection classifier)
        {
            Console.WriteLine();
            Console.Write("Path to a UTF-8 text file (one message per line): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { return; }

            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {path}");
                Console.ResetColor();
                return;
            }

            var messages = File.ReadAllLines(path, Encoding.UTF8)
                               .Select(l => l.Trim())
                               .Where(l => l.Length > 0)
                               .ToList();
            if (messages.Count == 0)
            {
                Console.WriteLine("File contained no non-empty lines.");
                return;
            }

            string label = $"File ({Path.GetFileName(path)}, {messages.Count} messages)";
            RunBatchMode(classifier, label, messages, promptCsv: true,
                         suggestedCsvName: Path.GetFileNameWithoutExtension(path) + "_triage.csv");
        }

        static void RunBatchMode(
            SarcasmDetection classifier,
            string label,
            List<string> messages,
            bool promptCsv,
            string suggestedCsvName = "triage.csv")
        {
            Console.WriteLine();
            Console.WriteLine($"--- {label} ---");
            Console.WriteLine();

            var rows = new List<TriageRow>(messages.Count);
            var latencies = new List<long>(messages.Count);

            for (int i = 0; i < messages.Count; i++)
            {
                string text = messages[i];
                Stopwatch sw = Stopwatch.StartNew();
                bool isSarc = classifier.IsSarcastic(text);
                sw.Stop();
                latencies.Add(sw.ElapsedMilliseconds);

                rows.Add(new TriageRow(
                    Id: i + 1, ClassifiedAt: DateTime.UtcNow,
                    IsSarcastic: isSarc, Confidence: classifier.Confidence,
                    LatencyMs: sw.ElapsedMilliseconds, Text: text));

                PrintResult(i + 1, text, isSarc, classifier.Confidence, sw.ElapsedMilliseconds);
            }

            Console.WriteLine();
            PrintSummary(rows, latencies);

            if (promptCsv)
            {
                Console.WriteLine();
                Console.Write($"Write triage CSV? [Y/n] (default file: {suggestedCsvName}) : ");
                string? answer = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(answer) || answer.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    string path = Path.GetFullPath(suggestedCsvName);
                    WriteCsv(rows, path);
                    Console.WriteLine($"Wrote {path}");
                }
            }
            Console.WriteLine();
        }

        static void PrintResult(int id, string text, bool isSarc, double confidence, long latencyMs)
        {
            ConsoleColor c = isSarc ? ConsoleColor.Yellow : ConsoleColor.Green;
            string verdict = isSarc ? "SARCASM" : "sincere";
            Console.ForegroundColor = c;
            Console.Write($"  [{id,3}] {verdict,-8}");
            Console.ResetColor();
            Console.WriteLine($" ({confidence:P0}, {latencyMs} ms)  {Truncate(text, 80)}");
        }

        static void PrintSummary(List<TriageRow> rows, List<long> latencies)
        {
            if (rows.Count == 0) { return; }
            int sarc = rows.Count(r => r.IsSarcastic);
            int sincere = rows.Count - sarc;
            latencies.Sort();
            long median = latencies[latencies.Count / 2];

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Summary");
            Console.ResetColor();
            Console.WriteLine($"  Total          : {rows.Count}");
            Console.WriteLine($"  Sarcastic      : {sarc} ({(rows.Count == 0 ? 0 : 100.0 * sarc / rows.Count):F1}%)");
            Console.WriteLine($"  Sincere        : {sincere}");
            Console.WriteLine($"  Median latency : {median} ms");

            var topSarc = rows.Where(r => r.IsSarcastic).OrderByDescending(r => r.Confidence).Take(3).ToList();
            if (topSarc.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Top suspected sarcasm (review first):");
                Console.ResetColor();
                foreach (var r in topSarc)
                {
                    Console.WriteLine($"    [{r.Id,3}] ({r.Confidence:P0}) {Truncate(r.Text, 80)}");
                }
            }
        }

        static List<string> BuiltInSample() => new()
        {
            "Oh great, another two-hour wait for support. Just fantastic.",
            "Thank you so much, the new dashboard saved my team hours every week.",
            "Wow, I love how the app crashes every time I open it. Truly innovative.",
            "Just renewed for another year. Best money I've spent on a tool.",
            "Sure, I'd love to update my password for the third time this month.",
            "The new export feature works exactly as I needed. Perfect timing.",
            "Riveting. A 47-page release note for a single bug fix.",
            "Customer service was responsive and resolved my issue in under an hour.",
            "Can't wait to read the 12-step migration guide for what should be a setting toggle.",
            "Honestly, the onboarding tutorial was the clearest I've ever seen.",
            "Yet another paywall behind a paywall. Subscription-ception.",
            "I appreciate you fixing the search bug so quickly. Thank you.",
        };

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║         Social-Media Sarcasm Triage              ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Detect sarcastic content with the LM-Kit fine-tuned classifier,");
            Console.WriteLine("flag it for human review, and export results to CSV.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Classify one message at a time (interactive)");
            Console.WriteLine("  2 / sample   Run the built-in 12-message sample dataset");
            Console.WriteLine("  3 / file     Classify every line of a UTF-8 text file");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }

        static void WriteCsv(List<TriageRow> rows, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("id,classified_at,is_sarcastic,confidence,latency_ms,text");
            foreach (var r in rows)
            {
                w.Write(r.Id); w.Write(",");
                w.Write(r.ClassifiedAt.ToString("o", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(r.IsSarcastic ? "true" : "false"); w.Write(",");
                w.Write(r.Confidence.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(r.LatencyMs); w.Write(",");
                w.Write(CsvEscape(r.Text));
                w.WriteLine();
            }
        }

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                Console.Write($"\rDownloading model {Math.Round((double)bytesRead / contentLength.Value * 100, 2):0.00}%");
            }
            return true;
        }

        static bool OnLoadProgress(float progress)
        {
            if (_isDownloading) { Console.WriteLine(); _isDownloading = false; }
            Console.Write($"\rLoading model {Math.Round(progress * 100)}%");
            return true;
        }
    }
}
