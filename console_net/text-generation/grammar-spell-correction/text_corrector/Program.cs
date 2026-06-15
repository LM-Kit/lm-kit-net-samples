using LMKit.Model;
using LMKit.TextEnhancement;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace text_corrector
{
    internal sealed record DiffLine(char Kind, string Text);

    internal sealed record FileReview(
        string Source, string CorrectedPath,
        int LinesAdded, int LinesRemoved, double EditRatio,
        double ElapsedSeconds, List<DiffLine> Diff);

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

            LM model = LoadModelInteractive();
            TextCorrection corrector = new(model);

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
                        RunLiveMode(corrector);
                        break;
                    case "2": case "sample":
                        RunSampleMode(corrector);
                        break;
                    case "3": case "file":
                        RunSingleFileMode(corrector);
                        break;
                    case "4": case "folder":
                        RunFolderMode(corrector);
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

        static void RunLiveMode(TextCorrection corrector)
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Type or paste a single line. Empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("text > ");
                Console.ResetColor();
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine(); return; }

                Stopwatch sw = Stopwatch.StartNew();
                string corrected = corrector.Correct(text, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                sw.Stop();

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  original  : {text}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  corrected : {corrected}");
                Console.ResetColor();
                Console.WriteLine($"  {sw.Elapsed.TotalSeconds:F1}s");
                Console.WriteLine();
            }
        }

        static void RunSampleMode(TextCorrection corrector)
        {
            string[] samples =
            {
                "Their going too the store later, becuase they need somthing for dinner.",
                "The manager have to decide weather the project should be cancelled or not.",
                "Me and Sara was happy when the results was announced yesterday.",
                "If you would of told me earlier, I could of prepared the report by now.",
                "The team is currently reviewing the proposal, and they will get back to you when its done.",
            };

            Console.WriteLine();
            Console.WriteLine("--- Built-in sample sentences ---");
            Console.WriteLine();
            foreach (string s in samples)
            {
                Stopwatch sw = Stopwatch.StartNew();
                string corrected = corrector.Correct(s, new CancellationTokenSource(TimeSpan.FromMinutes(2)).Token);
                sw.Stop();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  original  : {s}");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"  corrected : {corrected}");
                Console.ResetColor();
                Console.WriteLine($"  {sw.Elapsed.TotalSeconds:F1}s");
                Console.WriteLine();
            }
        }

        static void RunSingleFileMode(TextCorrection corrector)
        {
            Console.WriteLine();
            Console.Write("Path to a .txt or .md file: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { return; }
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {path}");
                Console.ResetColor();
                return;
            }
            Console.Write("Output path for the corrected file: ");
            string? outPath = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outPath))
            {
                outPath = Path.Combine(
                    Path.GetDirectoryName(path) ?? "",
                    Path.GetFileNameWithoutExtension(path) + ".corrected" + Path.GetExtension(path));
            }

            string original = File.ReadAllText(path, Encoding.UTF8);
            Stopwatch sw = Stopwatch.StartNew();
            string corrected = corrector.Correct(original, new CancellationTokenSource(TimeSpan.FromMinutes(10)).Token);
            sw.Stop();
            File.WriteAllText(outPath, corrected, Encoding.UTF8);

            var diff = ComputeLineDiff(original, corrected);
            int added = diff.Count(d => d.Kind == '+');
            int removed = diff.Count(d => d.Kind == '-');
            Console.WriteLine();
            Console.WriteLine($"Wrote {Path.GetFullPath(outPath)}");
            Console.WriteLine($"  +{added}  -{removed}  in {sw.Elapsed.TotalSeconds:F1}s");

            Console.Write("Show diff inline? [y/N]: ");
            string? show = Console.ReadLine()?.Trim();
            if (!string.IsNullOrEmpty(show) && show.StartsWith("y", StringComparison.OrdinalIgnoreCase))
            {
                PrintDiff(diff);
            }
            Console.WriteLine();
        }

        static void RunFolderMode(TextCorrection corrector)
        {
            Console.WriteLine();
            Console.Write("Path to a folder containing .txt / .md files: ");
            string? inDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(inDir) || !Directory.Exists(inDir))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Folder not found: {inDir}");
                Console.ResetColor();
                return;
            }
            Console.Write("Output folder for corrected copies and diffs: ");
            string? outDir = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(outDir))
            {
                outDir = Path.Combine(inDir, "reviewed");
            }
            Directory.CreateDirectory(outDir);
            string diffDir = Path.Combine(outDir, "diffs");
            Directory.CreateDirectory(diffDir);

            string[] files = Directory.EnumerateFiles(inDir, "*", SearchOption.AllDirectories)
                .Where(f => IsTextLike(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (files.Length == 0)
            {
                Console.WriteLine("No .txt / .md files found in the folder.");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Reviewing {files.Length} file(s) ...");
            Console.WriteLine();
            var reviews = new List<FileReview>(files.Length);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string rel = Path.GetRelativePath(inDir, path);
                Console.WriteLine($"  [{i + 1}/{files.Length}] {rel}");
                try
                {
                    string original = File.ReadAllText(path, Encoding.UTF8);
                    Stopwatch sw = Stopwatch.StartNew();
                    string corrected = corrector.Correct(original, new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                    sw.Stop();

                    string correctedPath = Path.Combine(outDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(correctedPath)!);
                    File.WriteAllText(correctedPath, corrected, Encoding.UTF8);

                    var diff = ComputeLineDiff(original, corrected);
                    int added = diff.Count(d => d.Kind == '+');
                    int removed = diff.Count(d => d.Kind == '-');
                    int origLines = SplitLines(original).Length;
                    int corrLines = SplitLines(corrected).Length;
                    double ratio = (origLines + corrLines) == 0
                        ? 0
                        : (double)(added + removed) / Math.Max(origLines, corrLines);

                    string diffPath = Path.Combine(diffDir, SafeRel(rel) + ".diff.md");
                    Directory.CreateDirectory(Path.GetDirectoryName(diffPath)!);
                    WriteDiffMarkdown(diff, rel, diffPath);

                    reviews.Add(new FileReview(path, correctedPath, added, removed, ratio, sw.Elapsed.TotalSeconds, diff));
                    Console.WriteLine($"        +{added} -{removed} ratio={ratio:P0} {sw.Elapsed.TotalSeconds:F1}s");
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
            Console.WriteLine("Top-edited files (review these first):");
            Console.ResetColor();
            foreach (var r in reviews.OrderByDescending(r => r.EditRatio).Take(10))
            {
                string rel = Path.GetRelativePath(inDir, r.Source);
                Console.WriteLine($"  {rel,-50}  +{r.LinesAdded,3} -{r.LinesRemoved,3} ratio={r.EditRatio,7:P1}");
            }

            string reportPath = Path.Combine(outDir, "review_report.md");
            string csvPath = Path.Combine(outDir, "review_summary.csv");
            WriteReport(reviews, inDir, reportPath);
            WriteSummaryCsv(reviews, inDir, csvPath);
            Console.WriteLine();
            Console.WriteLine($"Markdown report : {Path.GetFullPath(reportPath)}");
            Console.WriteLine($"Summary CSV     : {Path.GetFullPath(csvPath)}");
            Console.WriteLine($"Diff folder     : {Path.GetFullPath(diffDir)}");
            Console.WriteLine();
        }

        static LM LoadModelInteractive()
        {
            Console.WriteLine("Select a model:");
            Console.WriteLine("  1 - Alibaba Qwen 3.5 9B          (~7 GB VRAM) [Recommended]");
            Console.WriteLine("  2 - Google Gemma 4 E4B           (~6 GB VRAM)");
            Console.WriteLine("  3 - Microsoft Phi-4 14.7B        (~11 GB VRAM)");
            Console.WriteLine("  4 - OpenAI GPT OSS 20B           (~16 GB VRAM)");
            Console.WriteLine("  5 - Z.ai GLM 4.7 Flash 30B      (~18 GB VRAM)");
            Console.WriteLine("  6 - Alibaba Qwen 3.6 27B         (~18 GB VRAM)");
            Console.WriteLine("  7 - Alibaba Qwen 3.6 35B-A3B     (~22 GB VRAM)");
            Console.WriteLine("  8 - Google Gemma 4 26B-A4B       (~18 GB VRAM)");
            Console.Write("\nOr enter a custom model URI\n> ");

            string input = Console.ReadLine()?.Trim() ?? "1";
            string? modelId = input switch
            {
                "1" => "qwen3.5:9b",
                "2" => "gemma4:e4b",
                "3" => "phi4",
                "4" => "gptoss:20b",
                "5" => "glm4.7-flash",
                "6" => "qwen3.6:27b",
                "7" => "qwen3.6:35b-a3b",
                "8" => "gemma4:26b-a4b",
                _ => null,
            };
            if (modelId != null)
            {
                return LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
            }
            return new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);
        }

        static void PrintDiff(List<DiffLine> diff)
        {
            foreach (var d in diff)
            {
                ConsoleColor c = d.Kind switch
                {
                    '+' => ConsoleColor.Green,
                    '-' => ConsoleColor.Red,
                    _ => ConsoleColor.DarkGray,
                };
                Console.ForegroundColor = c;
                Console.WriteLine($"{d.Kind} {d.Text}");
                Console.ResetColor();
            }
        }

        static List<DiffLine> ComputeLineDiff(string a, string b)
        {
            string[] left = SplitLines(a);
            string[] right = SplitLines(b);
            int m = left.Length, n = right.Length;
            var lcs = new int[m + 1, n + 1];
            for (int i = m - 1; i >= 0; i--)
            {
                for (int j = n - 1; j >= 0; j--)
                {
                    lcs[i, j] = left[i] == right[j]
                        ? lcs[i + 1, j + 1] + 1
                        : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);
                }
            }
            var diff = new List<DiffLine>();
            int x = 0, y = 0;
            while (x < m && y < n)
            {
                if (left[x] == right[y]) { diff.Add(new DiffLine(' ', left[x])); x++; y++; }
                else if (lcs[x + 1, y] >= lcs[x, y + 1]) { diff.Add(new DiffLine('-', left[x])); x++; }
                else { diff.Add(new DiffLine('+', right[y])); y++; }
            }
            while (x < m) { diff.Add(new DiffLine('-', left[x++])); }
            while (y < n) { diff.Add(new DiffLine('+', right[y++])); }
            return diff;
        }

        static void WriteDiffMarkdown(List<DiffLine> diff, string sourceRel, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine($"# Diff - `{sourceRel}`");
            w.WriteLine();
            w.WriteLine("```diff");
            foreach (var d in diff)
            {
                w.WriteLine(d.Kind == ' ' ? "  " + d.Text : d.Kind + " " + d.Text);
            }
            w.WriteLine("```");
        }

        static void WriteReport(List<FileReview> reviews, string inDir, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("# Grammar Review Report");
            w.WriteLine();
            w.WriteLine($"- Date: {DateTime.UtcNow:o}");
            w.WriteLine($"- Files reviewed: {reviews.Count}");
            w.WriteLine($"- Total lines changed: {reviews.Sum(r => r.LinesAdded + r.LinesRemoved)}");
            w.WriteLine();
            w.WriteLine("## Files by edit ratio (worst first)");
            w.WriteLine();
            w.WriteLine("| File | + | – | Ratio | Time |");
            w.WriteLine("|---|---:|---:|---:|---:|");
            foreach (var r in reviews.OrderByDescending(r => r.EditRatio))
            {
                string rel = Path.GetRelativePath(inDir, r.Source);
                w.WriteLine($"| `{rel}` | {r.LinesAdded} | {r.LinesRemoved} | {r.EditRatio:P1} | {r.ElapsedSeconds:F1}s |");
            }
        }

        static void WriteSummaryCsv(List<FileReview> reviews, string inDir, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("file,added,removed,edit_ratio,elapsed_seconds");
            foreach (var r in reviews)
            {
                string rel = Path.GetRelativePath(inDir, r.Source);
                w.Write(CsvEscape(rel)); w.Write(",");
                w.Write(r.LinesAdded); w.Write(",");
                w.Write(r.LinesRemoved); w.Write(",");
                w.Write(r.EditRatio.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(r.ElapsedSeconds.ToString("F2", CultureInfo.InvariantCulture));
                w.WriteLine();
            }
        }

        static bool IsTextLike(string path) =>
            Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md";

        static string SafeRel(string rel) =>
            rel.Replace(Path.DirectorySeparatorChar, '_').Replace(Path.AltDirectorySeparatorChar, '_');

        static string[] SplitLines(string s) =>
            s.Replace("\r\n", "\n").Split('\n');

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Folder Grammar Reviewer with Diff           ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Correct grammar, generate per-file diffs, rank files by edit ratio.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Correct one line at a time (interactive)");
            Console.WriteLine("  2 / sample   Run 5 built-in sample sentences");
            Console.WriteLine("  3 / file     Correct a single .txt / .md file with diff");
            Console.WriteLine("  4 / folder   Review a folder, produce per-file diffs and a summary");
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
