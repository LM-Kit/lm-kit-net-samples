using LMKit.Model;
using LMKit.TextAnalysis;
using System.Globalization;
using System.Text;

namespace sentiment_analysis
{
    internal sealed record Review(int Id, string Product, string Source, string Date, string Text);
    internal sealed record ClassifiedReview(Review Review, string Category, double Confidence);

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

            Console.WriteLine("Loading lmkit-sentiment-analysis (fine-tuned English classifier) ...");
            using LM model = LM.LoadFromModelID(
                "lmkit-sentiment-analysis",
                downloadingProgress: OnDownloadProgress,
                loadingProgress: OnLoadProgress);
            Console.WriteLine();

            bool neutralSupport = PromptYesNo("Enable 3-class (positive / neutral / negative) instead of binary?", defaultYes: false);
            SentimentAnalysis classifier = new(model) { NeutralSupport = neutralSupport };

            Console.Clear();
            WriteHeader();
            Console.WriteLine($"Classifier mode: {(neutralSupport ? "3-class (pos / neu / neg)" : "binary (pos / neg)")}");
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
                        RunBatch(classifier, "Built-in sample (12 reviews)", BuiltInReviews(), promptCsv: true);
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

        static void RunLiveMode(SentimentAnalysis classifier)
        {
            Console.WriteLine();
            Console.WriteLine("Live mode. Type a review and press enter. Empty line returns to menu.");
            Console.WriteLine();
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write("review > ");
                Console.ResetColor();
                string? text = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(text)) { Console.WriteLine(); return; }

                var category = classifier.GetSentimentCategory(text);
                PrintResult(category.ToString(), classifier.Confidence, text);
                Console.WriteLine();
            }
        }

        static void RunFileMode(SentimentAnalysis classifier)
        {
            Console.WriteLine();
            Console.WriteLine("File mode supports two formats:");
            Console.WriteLine("  (a) Plain text: one review per line (UTF-8).");
            Console.WriteLine("  (b) CSV with header columns: id, product, source, date, text");
            Console.WriteLine();
            Console.Write("Path to file: ");
            string? path = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path)) { return; }
            if (!File.Exists(path))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"File not found: {path}");
                Console.ResetColor();
                return;
            }

            List<Review> reviews = path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
                ? ReadCsv(path)
                : ReadFlatLines(path);

            if (reviews.Count == 0)
            {
                Console.WriteLine("File contains no usable reviews.");
                return;
            }

            string label = $"File ({Path.GetFileName(path)}, {reviews.Count} reviews)";
            RunBatch(classifier, label, reviews, promptCsv: true,
                     suggestedCsvName: Path.GetFileNameWithoutExtension(path) + "_classified.csv");
        }

        static void RunBatch(
            SentimentAnalysis classifier,
            string label,
            List<Review> reviews,
            bool promptCsv,
            string suggestedCsvName = "feedback_classified.csv")
        {
            Console.WriteLine();
            Console.WriteLine($"--- {label} ---");
            Console.WriteLine();

            var results = new List<ClassifiedReview>(reviews.Count);
            for (int i = 0; i < reviews.Count; i++)
            {
                var r = reviews[i];
                var category = classifier.GetSentimentCategory(r.Text);
                results.Add(new ClassifiedReview(r, category.ToString(), classifier.Confidence));
                if ((i + 1) % 25 == 0 || i == reviews.Count - 1)
                {
                    Console.Write($"\r  classified {i + 1}/{reviews.Count}");
                }
            }
            Console.WriteLine();
            Console.WriteLine();

            PrintDashboard(results);

            if (promptCsv)
            {
                Console.WriteLine();
                Console.Write($"Write per-row CSV? [Y/n] (default file: {suggestedCsvName}) : ");
                string? answer = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(answer) || answer.Equals("y", StringComparison.OrdinalIgnoreCase))
                {
                    string rowsPath = Path.GetFullPath(suggestedCsvName);
                    string segPath = Path.GetFullPath(Path.GetFileNameWithoutExtension(suggestedCsvName) + "_summary.csv");
                    WriteRowsCsv(results, rowsPath);
                    WriteSegmentSummaryCsv(results, segPath);
                    Console.WriteLine($"Wrote {rowsPath}");
                    Console.WriteLine($"Wrote {segPath}");
                }
            }
            Console.WriteLine();
        }

        static void PrintDashboard(List<ClassifiedReview> results)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Dashboard");
            Console.ResetColor();

            int total = results.Count;
            int neg = results.Count(r => string.Equals(r.Category, "Negative", StringComparison.OrdinalIgnoreCase));
            int pos = results.Count(r => string.Equals(r.Category, "Positive", StringComparison.OrdinalIgnoreCase));
            int neu = total - neg - pos;
            Console.WriteLine($"  Overall: {total} reviews   pos={pos} ({Pct(pos, total):F1}%)   " +
                              $"neu={neu} ({Pct(neu, total):F1}%)   neg={neg} ({Pct(neg, total):F1}%)");

            var byProduct = results.GroupBy(r => r.Review.Product)
                                   .Select(g => Segment(g.Key, "product", g.ToList()))
                                   .OrderByDescending(s => s.NegativeRate)
                                   .ToList();
            Console.WriteLine();
            Console.WriteLine("  By product (worst first):");
            PrintTable(byProduct);

            var bySource = results.GroupBy(r => r.Review.Source)
                                  .Select(g => Segment(g.Key, "source", g.ToList()))
                                  .OrderByDescending(s => s.NegativeRate)
                                  .ToList();
            Console.WriteLine();
            Console.WriteLine("  By source (worst first):");
            PrintTable(bySource);

            var topNeg = results
                .Where(r => string.Equals(r.Category, "Negative", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(r => r.Confidence)
                .Take(5)
                .ToList();
            if (topNeg.Count > 0)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("  Strongest negatives:");
                Console.ResetColor();
                foreach (var r in topNeg)
                {
                    Console.WriteLine($"    #{r.Review.Id,3} [{r.Review.Product}/{r.Review.Source}] " +
                                      $"({r.Confidence:P0})  {Truncate(r.Review.Text, 80)}");
                }
            }
        }

        record SegmentRow(string Name, string Dimension, int Total, int Positive, int Negative, int Neutral, double NegativeRate);

        static SegmentRow Segment(string name, string dim, List<ClassifiedReview> rows)
        {
            int total = rows.Count;
            int p = rows.Count(r => string.Equals(r.Category, "Positive", StringComparison.OrdinalIgnoreCase));
            int n = rows.Count(r => string.Equals(r.Category, "Negative", StringComparison.OrdinalIgnoreCase));
            return new SegmentRow(name, dim, total, p, n, total - p - n, total == 0 ? 0 : (double)n / total);
        }

        static void PrintTable(List<SegmentRow> rows)
        {
            Console.WriteLine($"    {"Segment",-20} {"Total",6} {"Pos",6} {"Neu",6} {"Neg",6} {"Neg%",8}");
            foreach (var s in rows)
            {
                Console.WriteLine($"    {Truncate(s.Name, 20),-20} {s.Total,6} {s.Positive,6} {s.Neutral,6} {s.Negative,6} {s.NegativeRate,8:P1}");
            }
        }

        static void PrintResult(string category, double confidence, string text)
        {
            ConsoleColor c = category.ToLowerInvariant() switch
            {
                "positive" => ConsoleColor.Green,
                "negative" => ConsoleColor.Red,
                _ => ConsoleColor.Yellow,
            };
            Console.ForegroundColor = c;
            Console.Write($"  {category,-10}");
            Console.ResetColor();
            Console.WriteLine($" ({confidence:P0})  {Truncate(text, 80)}");
        }

        static List<Review> ReadFlatLines(string path)
        {
            var reviews = new List<Review>();
            int id = 0;
            foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
            {
                string t = line.Trim();
                if (t.Length == 0) { continue; }
                id++;
                reviews.Add(new Review(id, "(unknown)", "(unknown)", "", t));
            }
            return reviews;
        }

        static List<Review> ReadCsv(string path)
        {
            var reviews = new List<Review>();
            using var r = new StreamReader(path, Encoding.UTF8);
            string? header = r.ReadLine();
            string? row;
            int line = 1;
            while ((row = r.ReadLine()) != null)
            {
                line++;
                if (string.IsNullOrWhiteSpace(row)) { continue; }
                string[] cols = ParseCsvLine(row);
                if (cols.Length < 5) { continue; }
                if (!int.TryParse(cols[0], out int id)) { id = line; }
                reviews.Add(new Review(id, cols[1].Trim(), cols[2].Trim(), cols[3].Trim(), cols[4].Trim()));
            }
            return reviews;
        }

        static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"' && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else if (c == '"') { inQuotes = false; }
                    else { sb.Append(c); }
                }
                else
                {
                    if (c == ',') { fields.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '"' && sb.Length == 0) { inQuotes = true; }
                    else { sb.Append(c); }
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        static List<Review> BuiltInReviews() => new()
        {
            new(1,  "Web App",     "App Store",  "2026-05-01", "Crashes on every search. Unusable since the last update."),
            new(2,  "Web App",     "App Store",  "2026-05-01", "Smooth experience and fast results. Recommend it."),
            new(3,  "Web App",     "Survey",     "2026-05-02", "Pricing tripled overnight and the new tier dropped features I rely on."),
            new(4,  "Web App",     "Survey",     "2026-05-03", "Customer support solved my issue in under an hour. Great team."),
            new(5,  "Web App",     "Twitter",    "2026-05-03", "Still cannot export reports. Two months and the bug is open."),
            new(6,  "Mobile App",  "App Store",  "2026-05-01", "Beautiful UI, intuitive flows, offline sync works flawlessly."),
            new(7,  "Mobile App",  "App Store",  "2026-05-02", "Battery drain through the roof since v3.2."),
            new(8,  "Mobile App",  "Survey",     "2026-05-02", "Sync conflicts between phone and desktop, lost an hour of edits."),
            new(9,  "Mobile App",  "Survey",     "2026-05-04", "Productivity boost. The widget alone is worth it."),
            new(10, "Mobile App",  "Twitter",    "2026-05-04", "Onboarding tutorial is the clearest I have ever seen in a fitness app."),
            new(11, "Web App",     "Twitter",    "2026-05-04", "Honestly the dashboard is fine, just wish search had filters."),
            new(12, "Mobile App",  "Twitter",    "2026-05-05", "It is okay. Does what it says. Nothing special."),
        };

        static void WriteRowsCsv(List<ClassifiedReview> results, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("id,product,source,date,category,confidence,text");
            foreach (var r in results)
            {
                w.Write(r.Review.Id); w.Write(",");
                w.Write(CsvEscape(r.Review.Product)); w.Write(",");
                w.Write(CsvEscape(r.Review.Source)); w.Write(",");
                w.Write(CsvEscape(r.Review.Date)); w.Write(",");
                w.Write(r.Category); w.Write(",");
                w.Write(r.Confidence.ToString("F4", CultureInfo.InvariantCulture)); w.Write(",");
                w.Write(CsvEscape(r.Review.Text));
                w.WriteLine();
            }
        }

        static void WriteSegmentSummaryCsv(List<ClassifiedReview> results, string path)
        {
            using var w = new StreamWriter(path, false, Encoding.UTF8);
            w.WriteLine("dimension,segment,total,positive,neutral,negative,negative_rate");
            foreach (string dim in new[] { "product", "source" })
            {
                var groups = results.GroupBy(r => dim == "product" ? r.Review.Product : r.Review.Source);
                foreach (var g in groups)
                {
                    var s = Segment(g.Key, dim, g.ToList());
                    w.Write(s.Dimension); w.Write(",");
                    w.Write(CsvEscape(s.Name)); w.Write(",");
                    w.Write(s.Total); w.Write(",");
                    w.Write(s.Positive); w.Write(",");
                    w.Write(s.Neutral); w.Write(",");
                    w.Write(s.Negative); w.Write(",");
                    w.Write(s.NegativeRate.ToString("F4", CultureInfo.InvariantCulture));
                    w.WriteLine();
                }
            }
        }

        static void WriteHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔══════════════════════════════════════════════════╗");
            Console.WriteLine("║      Customer Feedback Sentiment Dashboard       ║");
            Console.WriteLine("╚══════════════════════════════════════════════════╝");
            Console.ResetColor();
            Console.WriteLine("Classify reviews, segment by product/source, surface the worst.");
            Console.WriteLine();
        }

        static void PrintMenu()
        {
            Console.WriteLine();
            Console.WriteLine("  1 / live     Classify one review at a time (interactive)");
            Console.WriteLine("  2 / sample   Run the built-in 12-review dataset");
            Console.WriteLine("  3 / file     Classify a flat .txt or a structured .csv file");
            Console.WriteLine("  q / quit     Exit");
            Console.WriteLine();
        }

        static bool PromptYesNo(string question, bool defaultYes)
        {
            string defaultLabel = defaultYes ? "[Y/n]" : "[y/N]";
            Console.Write($"{question} {defaultLabel}: ");
            string? a = Console.ReadLine()?.Trim();
            if (string.IsNullOrEmpty(a)) { return defaultYes; }
            return a.StartsWith("y", StringComparison.OrdinalIgnoreCase);
        }

        static string CsvEscape(string s)
            => s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;

        static double Pct(int n, int total) => total == 0 ? 0 : 100.0 * n / total;

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
