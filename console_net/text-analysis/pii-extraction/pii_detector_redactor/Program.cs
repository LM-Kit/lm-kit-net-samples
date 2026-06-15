using LMKit.Data;
using LMKit.Model;
using LMKit.TextAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace pii_extraction
{
    internal enum RedactionMode { Mask, Label, Hash }

    internal sealed record AuditEntry(
        string SourcePath, string Label, string Original, string Redaction,
        int StartIndex, int EndIndex, double Confidence);

    internal class Program
    {
        static bool _isDownloading;

        static readonly HashSet<string> TextExt = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".eml", ".log" };

        static void Main()
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            Console.Clear();
            WriteHeader();

            LM model = LoadModelInteractive();
            PiiExtraction engine = new(model);

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
                        RunLiveMode(engine);
                        break;
                    case "2": case "file":
                        RunSingleFileMode(engine);
                        break;
                    case "3": case "folder":
                        RunFolderMode(engine);
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

        static void RunLiveMode(PiiExtraction engine)
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Paste text; empty line returns to menu.");
            Console.WriteLine();
            RedactionMode mode = PromptRedactionMode();

            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("text > ");
                Console.ResetColor();
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine(); return; }

                var entities = engine.Extract(text);
                var spans = CollectSpans(entities, text, 0.0);
                spans = ResolveOverlaps(spans);

                string redacted = ApplySpans(text, spans, mode);
                Console.WriteLine();
                Console.WriteLine($"  Found {entities.Count} PII entity(ies), {spans.Count} occurrence(s):");
                foreach (var s in spans.OrderBy(s => s.Start))
                {
                    Console.WriteLine($"    [{s.Label,-14}] @{s.Start,-5} {Truncate(s.Original, 50)} -> {BuildReplacement(s.Label, s.Original, mode)}");
                }
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  redacted: {Truncate(redacted, 200)}");
                Console.ResetColor();
                Console.WriteLine();
            }
        }

        static void RunSingleFileMode(PiiExtraction engine)
        {
            Console.WriteLine();
            Console.Write("Path to a text file (.txt .md .eml .log): ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { return; }
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {path}");
                Console.ResetColor();
                return;
            }

            RedactionMode mode = PromptRedactionMode();
            Console.Write("Output path for the redacted file: ");
            string? outPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outPath))
            {
                outPath = Path.Combine(
                    Path.GetDirectoryName(path) ?? "",
                    Path.GetFileNameWithoutExtension(path) + ".redacted" + Path.GetExtension(path));
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            using var att = new Attachment(path);
            var entities = engine.Extract(att);
            var spans = CollectSpans(entities, text, 0.0);
            spans = ResolveOverlaps(spans);
            string redacted = ApplySpans(text, spans, mode);
            File.WriteAllText(outPath, redacted, Encoding.UTF8);

            Console.WriteLine();
            Console.WriteLine($"Wrote {Path.GetFullPath(outPath)}");
            Console.WriteLine($"  {spans.Count} occurrence(s) redacted across {entities.Count} distinct entity value(s).");

            string auditPath = Path.Combine(
                Path.GetDirectoryName(outPath) ?? "",
                Path.GetFileNameWithoutExtension(outPath) + "_audit.csv");
            var audit = spans.Select(s => new AuditEntry(
                path, s.Label, s.Original, BuildReplacement(s.Label, s.Original, mode),
                s.Start, s.End, s.Confidence)).ToList();
            WriteAuditCsv(audit, auditPath);
            Console.WriteLine($"Audit log : {Path.GetFullPath(auditPath)}");
            Console.WriteLine();
        }

        static void RunFolderMode(PiiExtraction engine)
        {
            Console.WriteLine();
            Console.Write("Path to a folder of text files: ");
            string? inDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Folder not found: {inDir}");
                Console.ResetColor();
                return;
            }
            RedactionMode mode = PromptRedactionMode();
            Console.Write("Minimum confidence threshold [0.0]: ");
            string? mcStr = Console.ReadLine()?.Trim();
            double minConfidence = double.TryParse(mcStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double mc) ? mc : 0.0;
            Console.Write("Output folder for redacted copies [default: <input>/_redacted]: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir)) { outDir = Path.Combine(inDir, "_redacted"); }
            Directory.CreateDirectory(outDir);

            string[] files = Directory.EnumerateFiles(inDir, "*", SearchOption.AllDirectories)
                .Where(f => TextExt.Contains(Path.GetExtension(f)))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                Console.WriteLine("No .txt / .md / .eml / .log files found.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Redacting {files.Length} file(s) with mode={mode} (min-conf={minConfidence:P0}) ...");
            Console.WriteLine();
            var audit = new List<AuditEntry>();
            var perLabel = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string rel = Path.GetRelativePath(inDir, path);
                string outPath = Path.Combine(outDir, rel);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
                Console.WriteLine($"  [{i + 1}/{files.Length}] {rel}");
                try
                {
                    string text = File.ReadAllText(path, Encoding.UTF8);
                    using var att = new Attachment(path);
                    var entities = engine.Extract(att);
                    var spans = CollectSpans(entities, text, minConfidence);
                    spans = ResolveOverlaps(spans);
                    string redacted = ApplySpans(text, spans, mode);
                    File.WriteAllText(outPath, redacted, Encoding.UTF8);

                    foreach (var s in spans)
                    {
                        audit.Add(new AuditEntry(
                            path, s.Label, s.Original, BuildReplacement(s.Label, s.Original, mode),
                            s.Start, s.End, s.Confidence));
                        perLabel.TryGetValue(s.Label, out int n);
                        perLabel[s.Label] = n + 1;
                    }
                    Console.WriteLine($"        {spans.Count} redaction(s) -> {Path.GetRelativePath(outDir, outPath)}");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"        [error] {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Redaction summary by label:");
            Console.ResetColor();
            foreach (var kv in perLabel.OrderByDescending(p => p.Value))
            {
                Console.WriteLine($"  {kv.Key,-20} {kv.Value,6}");
            }

            string auditPath = Path.Combine(outDir, "redaction_audit.csv");
            WriteAuditCsv(audit, auditPath);
            Console.WriteLine();
            Console.WriteLine($"Audit log: {Path.GetFullPath(auditPath)}");
            Console.WriteLine();
        }

        // ── span collection / overlap / apply ──

        static List<(int Start, int End, string Label, string Original, double Confidence)>
            CollectSpans(IList<PiiExtraction.PiiExtractedEntity> entities, string text, double minConfidence)
        {
            var spans = new List<(int Start, int End, string Label, string Original, double Confidence)>();
            foreach (var e in entities)
            {
                if (e.Confidence < minConfidence) { continue; }
                foreach (var occ in e.Occurrences)
                {
                    if (occ.EndIndex <= occ.StartIndex) { continue; }
                    if (occ.StartIndex < 0 || occ.EndIndex > text.Length) { continue; }
                    spans.Add((
                        Start: occ.StartIndex,
                        End: occ.EndIndex,
                        Label: e.EntityDefinition.Label,
                        Original: text.Substring(occ.StartIndex, occ.EndIndex - occ.StartIndex),
                        Confidence: e.Confidence));
                }
            }
            return spans;
        }

        static List<(int Start, int End, string Label, string Original, double Confidence)>
            ResolveOverlaps(List<(int Start, int End, string Label, string Original, double Confidence)> spans)
        {
            var sorted = spans
                .OrderByDescending(s => s.End - s.Start)
                .ThenByDescending(s => s.Confidence)
                .ToList();
            var kept = new List<(int Start, int End, string Label, string Original, double Confidence)>();
            foreach (var s in sorted)
            {
                bool overlapsKept = kept.Any(k => !(s.End <= k.Start || s.Start >= k.End));
                if (!overlapsKept) { kept.Add(s); }
            }
            return kept;
        }

        static string ApplySpans(
            string text,
            List<(int Start, int End, string Label, string Original, double Confidence)> spans,
            RedactionMode mode)
        {
            var sb = new StringBuilder(text);
            foreach (var s in spans.OrderByDescending(s => s.Start))
            {
                string replacement = BuildReplacement(s.Label, s.Original, mode);
                sb.Remove(s.Start, s.End - s.Start);
                sb.Insert(s.Start, replacement);
            }
            return sb.ToString();
        }

        static string BuildReplacement(string label, string value, RedactionMode mode) =>
            mode switch
            {
                RedactionMode.Mask  => MaskPreservingShape(value),
                RedactionMode.Label => $"[{label.ToUpperInvariant()}]",
                RedactionMode.Hash  => $"[{label.ToUpperInvariant()}#{ShortHash(value.ToLowerInvariant().Trim())}]",
                _ => "[REDACTED]",
            };

        static string MaskPreservingShape(string value)
        {
            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                sb.Append(char.IsWhiteSpace(c) ? c : char.IsDigit(c) ? '#' : '*');
            }
            return sb.ToString();
        }

        static string ShortHash(string s)
        {
            using var sha = SHA1.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
            return Convert.ToHexString(hash).ToLowerInvariant().Substring(0, 6);
        }

        // ── prompts ──

        static RedactionMode PromptRedactionMode()
        {
            Console.Write("Redaction mode? [1=mask (default), 2=label, 3=hash]: ");
            string? a = Console.ReadLine()?.Trim();
            return a switch
            {
                "2" or "label" => RedactionMode.Label,
                "3" or "hash" => RedactionMode.Hash,
                _ => RedactionMode.Mask,
            };
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 2B          (~2 GB VRAM) [Fast]");
            Console.WriteLine("  2 - Alibaba Qwen 3.5 4B          (~3.5 GB VRAM)");
            Console.WriteLine("  3 - Alibaba Qwen 3.5 9B          (~7 GB VRAM) [Recommended]");
            Console.WriteLine("  4 - Google Gemma 4 E4B           (~6 GB VRAM)");
            Console.WriteLine("  5 - Alibaba Qwen 3.6 27B         (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.6 35B-A3B     (~22 GB VRAM)");
            Console.WriteLine("  7 - Google Gemma 4 26B-A4B       (~18 GB VRAM)");
            Console.Write("\nOr enter a custom model URI\n> ");
            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3.5:2b",
                "2" => "qwen3.5:4b",
                "3" => "qwen3.5:9b",
                "4" => "gemma4:e4b",
                "5" => "qwen3.6:27b",
                "6" => "qwen3.6:35b-a3b",
                "7" => "gemma4:26b-a4b",
                _ => null,
            };
            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        // ── audit ──

        static void WriteAuditCsv(List<AuditEntry> entries, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("source,label,original,redaction,start,end,confidence");
            foreach (var e in entries)
            {
                w.Write(CsvEscape(e.SourcePath)); w.Write(",");
                w.Write(CsvEscape(e.Label)); w.Write(",");
                w.Write(CsvEscape(e.Original)); w.Write(",");
                w.Write(CsvEscape(e.Redaction)); w.Write(",");
                w.Write(e.StartIndex); w.Write(",");
                w.Write(e.EndIndex); w.Write(",");
                w.Write(e.Confidence.ToString("F4", CultureInfo.InvariantCulture));
                w.WriteLine();
            }
        }

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static string Truncate(string s, int max)
            => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Folder PII Redaction Tool                   ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Detect PII with position-accurate spans, redact in three modes,");
            Console.WriteLine("write redacted copies and a full audit trail.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Detect + redact pasted text (interactive)");
            Console.WriteLine("  2 / file     Redact a single text file");
            Console.WriteLine("  3 / folder   Redact a folder of text files + audit CSV");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }

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
