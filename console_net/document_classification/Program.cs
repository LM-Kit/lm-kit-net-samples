using LMKit.Data;
using LMKit.Model;
using LMKit.TextAnalysis;
using System.Diagnostics;
using System.Text;

namespace document_classification
{
    internal class Program
    {
        private static bool _isDownloading;

        private static readonly string[] SupportedExtensions =
            { ".png", ".bmp", ".gif", ".psd", ".pic", ".jpeg", ".jpg", ".pnm", ".hdr",
              ".tga", ".webp", ".tiff", ".txt", ".html", ".pdf", ".docx", ".xlsx", ".pptx" };

        private static readonly List<string> Categories = new()
        {
            "invoice",
            "passport",
            "driver_license",
            "bank_statement",
            "tax_form",
            "receipt",
            "contract",
            "resume",
            "medical_record",
            "insurance_claim",
            "purchase_order",
            "shipping_label",
            "company_registration",
            "utility_bill",
            "pay_stub",
            "business_card",
            "id_card",
            "birth_certificate",
            "marriage_certificate",
            "loan_application",
            "check",
            "letter"
        };

        static void Main(string[] args)
        {
            // Set an optional license key here if available. 
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            LM model = LoadModel();
            Console.Clear();

            PrintHeader("Document Classification Demo");
            PrintModelInfo(model);

            var categorizer = new Categorization(model)
            {
                AllowUnknownCategory = true
            };

            PrintAvailableCategories();
            PrintHelp();

            while (true)
            {
                Console.WriteLine();
                Console.Write("Enter path (or command): ");
                string input = Console.ReadLine()?.Trim().Trim('"') ?? "";

                if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }

                if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
                {
                    PrintHelp();
                    continue;
                }

                if (input.Equals("categories", StringComparison.OrdinalIgnoreCase))
                {
                    PrintAvailableCategories();
                    continue;
                }

                if (input.Equals("clear", StringComparison.OrdinalIgnoreCase))
                {
                    Console.Clear();
                    PrintHeader("Document Classification Demo");
                    continue;
                }

                ProcessPath(input, categorizer);
            }

            Console.WriteLine("\nGoodbye! Press any key to exit.");
            Console.ReadKey();
        }

        private static void ProcessPath(string path, Categorization categorizer)
        {
            if (Directory.Exists(path))
            {
                ProcessDirectory(path, categorizer);
            }
            else if (File.Exists(path))
            {
                ClassifyDocument(path, categorizer);
            }
            else
            {
                WriteColored($"Path not found: {path}", ConsoleColor.Red);
            }
        }

        private static void ProcessDirectory(string directoryPath, Categorization categorizer)
        {
            var files = Directory.GetFiles(directoryPath)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (files.Count == 0)
            {
                WriteColored("No supported documents found in directory.", ConsoleColor.Yellow);
                return;
            }

            Console.WriteLine();
            WriteColored($"Processing {files.Count} document(s)...", ConsoleColor.Cyan);
            Console.WriteLine(new string('─', 70));

            var results = new List<(string File, string Category, double Confidence, long TimeMs)>();
            var totalSw = Stopwatch.StartNew();

            foreach (string file in files)
            {
                var result = ClassifyDocument(file, categorizer, isBatch: true);
                if (result.HasValue)
                {
                    results.Add((Path.GetFileName(file), result.Value.Category, result.Value.Confidence, result.Value.TimeMs));
                }
            }

            totalSw.Stop();

            // Print summary
            Console.WriteLine(new string('─', 70));
            WriteColored($"Batch complete: {results.Count}/{files.Count} processed in {totalSw.ElapsedMilliseconds:N0} ms", ConsoleColor.Cyan);

            if (results.Count > 0)
            {
                var grouped = results.GroupBy(r => r.Category)
                    .OrderByDescending(g => g.Count())
                    .ToList();

                Console.WriteLine("\nSummary by category:");
                foreach (var group in grouped)
                {
                    double avgConfidence = group.Average(r => r.Confidence);
                    Console.WriteLine($"  {group.Key,-20} : {group.Count(),3} document(s)  (avg confidence: {avgConfidence:P0})");
                }
            }
        }

        private static (string Category, double Confidence, long TimeMs)? ClassifyDocument(
            string filePath,
            Categorization categorizer,
            bool isBatch = false)
        {
            string fileName = Path.GetFileName(filePath);

            try
            {
                var sw = Stopwatch.StartNew();
                var attachment = new Attachment(filePath);
                int result = categorizer.GetBestCategory(Categories, attachment);
                sw.Stop();

                string category = result == -1 ? "unknown" : Categories[result];
                double confidence = categorizer.Confidence;

                PrintClassificationResult(fileName, category, confidence, sw.ElapsedMilliseconds, isBatch);

                return (category, confidence, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                if (isBatch)
                {
                    WriteColored($"  ✗ {fileName}: {ex.Message}", ConsoleColor.Red);
                }
                else
                {
                    WriteColored($"Error: {ex.Message}", ConsoleColor.Red);
                }
                return null;
            }
        }

        private static void PrintClassificationResult(string fileName, string category, double confidence, long elapsedMs, bool isBatch)
        {
            ConsoleColor confidenceColor = confidence switch
            {
                >= 0.8 => ConsoleColor.Green,
                >= 0.5 => ConsoleColor.Yellow,
                _ => ConsoleColor.Red
            };

            if (isBatch)
            {
                Console.Write("  ");
                WriteColored("✓ ", ConsoleColor.Green);
                Console.Write($"{TruncateFileName(fileName, 30),-32}");
                WriteColored($"{category,-20}", ConsoleColor.White);
                WriteColored($"{confidence,6:P0}", confidenceColor);
                Console.WriteLine($"  ({elapsedMs:N0} ms)");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine($"┌{"".PadRight(50, '─')}┐");
                Console.Write($"│ Category:   ");
                WriteColored($"{category,-36}", ConsoleColor.White);
                Console.WriteLine("│");
                Console.Write($"│ Confidence: ");
                WriteColored($"{confidence,-6:P0}", confidenceColor);
                Console.WriteLine($"{"",30}│");
                Console.WriteLine($"│ Time:       {elapsedMs:N0} ms{"",32}│".Substring(0, 52) + "│");
                Console.WriteLine($"└{"".PadRight(50, '─')}┘");
            }
        }

        private static string TruncateFileName(string fileName, int maxLength)
        {
            if (fileName.Length <= maxLength) return fileName;
            string ext = Path.GetExtension(fileName);
            int nameLength = maxLength - ext.Length - 3;
            return fileName.Substring(0, nameLength) + "..." + ext;
        }

        private static LM LoadModel()
        {
            Console.Clear();
            PrintHeader("Model Selection");

            var models = new (string Id, string Name, string Vram)[]
            {
                ("minicpm-o", "MiniCPM 2.6 o 8.1B", "~5.9 GB"),
                ("qwen3-vl:2b", "Alibaba Qwen 3 VL 2B", "~2.5 GB"),
                ("qwen3-vl:4b", "Alibaba Qwen 3 VL 4B", "~4.5 GB"),
                ("qwen3-vl:8b", "Alibaba Qwen 3 VL 8B", "~6.5 GB"),
                ("gemma3:4b", "Google Gemma 3 4B", "~5.7 GB"),
                ("gemma3:12b", "Google Gemma 3 12B", "~11 GB"),
                ("ministral3:3b", "Mistral Ministral 3 3B", "~3.5 GB"),
                ("ministral3:8b", "Mistral Ministral 3 8B", "~6.5 GB"),
                ("ministral3:14b", "Mistral Ministral 3 14B", "~12 GB")
            };

            Console.WriteLine("Available models:\n");
            for (int i = 0; i < models.Length; i++)
            {
                Console.WriteLine($"  [{i}] {models[i].Name,-30} (VRAM: {models[i].Vram})");
            }
            Console.WriteLine($"\n  [*] Enter a custom model URI");

            Console.Write("\nSelection: ");
            string input = Console.ReadLine()?.Trim() ?? "1";

            string modelLink;
            if (int.TryParse(input, out int index) && index >= 0 && index < models.Length)
            {
                modelLink = ModelCard.GetPredefinedModelCardByModelID(models[index].Id).ModelUri.ToString();
            }
            else
            {
                modelLink = input.Trim('"');
            }

            Console.WriteLine();

            return new LM(
                new Uri(modelLink),
                downloadingProgress: ModelDownloadingProgress,
                loadingProgress: ModelLoadingProgress);
        }

        private static bool ModelDownloadingProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            if (contentLength.HasValue)
            {
                double progressPercentage = (double)bytesRead / contentLength.Value * 100;
                string progressBar = CreateProgressBar(progressPercentage, 30);
                Console.Write($"\rDownloading: {progressBar} {progressPercentage:F1}%   ");
            }
            else
            {
                Console.Write($"\rDownloading: {bytesRead / 1024.0 / 1024.0:F1} MB   ");
            }
            return true;
        }

        private static bool ModelLoadingProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.WriteLine();
                _isDownloading = false;
            }

            string progressBar = CreateProgressBar(progress * 100, 30);
            Console.Write($"\rLoading:     {progressBar} {progress * 100:F0}%   ");
            return true;
        }

        private static string CreateProgressBar(double percentage, int width)
        {
            int filled = (int)(percentage / 100 * width);
            return "[" + new string('█', filled) + new string('░', width - filled) + "]";
        }

        private static void PrintHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"╔{"".PadRight(58, '═')}╗");
            Console.WriteLine($"║{title.PadLeft((58 + title.Length) / 2).PadRight(58)}║");
            Console.WriteLine($"╚{"".PadRight(58, '═')}╝");
            Console.ResetColor();
            Console.WriteLine();
        }

        private static void PrintModelInfo(LM model)
        {
            WriteColored("Model loaded: ", ConsoleColor.Gray);
            Console.WriteLine(Path.GetFileName(model.ModelUri.LocalPath));
            Console.WriteLine();
        }

        private static void PrintAvailableCategories()
        {
            Console.WriteLine("Supported categories:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            for (int i = 0; i < Categories.Count; i += 4)
            {
                var batch = Categories.Skip(i).Take(4);
                Console.WriteLine("  " + string.Join(", ", batch));
            }
            Console.ResetColor();
        }

        private static void PrintHelp()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("Commands: help | categories | clear | exit");
            Console.WriteLine("Tip: Enter a folder path to batch-process all documents");
            Console.WriteLine($"Supported formats: {string.Join(", ", SupportedExtensions.Select(e => e.TrimStart('.').ToUpper()))}");
            Console.ResetColor();
        }

        private static void WriteColored(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(text);
            Console.ResetColor();
        }
    }
}