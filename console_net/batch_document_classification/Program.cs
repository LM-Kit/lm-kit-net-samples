using LMKit.Data;
using LMKit.Model;
using LMKit.TextAnalysis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace batch_document_classification
{
    internal class Program
    {
        private static readonly string[] SupportedExtensions =
            { ".png", ".bmp", ".gif", ".psd", ".pic", ".jpeg", ".jpg", ".pnm", ".hdr",
              ".tga", ".webp", ".tiff", ".txt", ".html", ".pdf", ".docx", ".xlsx", ".pptx" };

        private static readonly List<string> Categories = new()
{
    "bank_details",
    "bank_statement",
    "birth_certificate",
    "business_card",
    "check",
    "company_registration",
    "contract",
    "driver_license",
    "id_card",
    "insurance_claim",
    "insurance_policy",
    "invoice",
    "letter",
    "loan_application",
    "marriage_certificate",
    "medical_record",
    "national_id",
    "passport",
    "pay_stub",
    "payment_card",
    "payroll_statement",
    "purchase_order",
    "receipt",
    "resume",
    "residence_permit",
    "shipping_document",
    "shipping_label",
    "tax_form",
    "unknown",
    "utility_bill"
};

        private static readonly object ConsoleLock = new();
        private static int _processed;
        private static int _total;
        private static Stopwatch _globalSw;

        static void Main(string[] args)
        {
            LMKit.Licensing.LicenseManager.SetLicenseKey("");
            Console.OutputEncoding = Encoding.UTF8;

            Console.WriteLine("=== Document Classification ===\n");

            // Load model
            Console.WriteLine("Loading model...");
            var model = new LM(
                ModelCard.GetPredefinedModelCardByModelID("lmkit-tasks:4b-preview").ModelUri,
                downloadingProgress: (p, c, b) => { Console.Write($"\rDownloading: {b / 1024 / 1024}MB   "); return true; },
                loadingProgress: p => { Console.Write($"\rLoading: {p * 100:F0}%   "); return true; });
            Console.WriteLine("\nModel loaded.\n");

            // Get input folder
            Console.Write("Input folder: ");
            string inputFolder = Console.ReadLine()?.Trim().Trim('"') ?? "";
            if (!Directory.Exists(inputFolder)) { Console.WriteLine("Folder not found."); return; }

            // Get output folder
            Console.Write("Output folder: ");
            string outputFolder = Console.ReadLine()?.Trim().Trim('"') ?? "";

            // Get thread count
            Console.Write($"Threads (1-{Environment.ProcessorCount}, default 1): ");
            string threadInput = Console.ReadLine()?.Trim() ?? "";
            int threads = int.TryParse(threadInput, out int t) && t >= 1 ? Math.Min(t, Environment.ProcessorCount) : 1;

            // Find files
            var files = Directory.GetFiles(inputFolder, "*.*", SearchOption.AllDirectories)
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToList();

            if (files.Count == 0) { Console.WriteLine("No supported files found."); return; }

            _total = files.Count;
            Console.WriteLine($"\nProcessing {_total} files with {threads} thread(s)...\n");

            var categorizers = new ConcurrentDictionary<int, Categorization>();
            var results = new ConcurrentBag<(string Category, double Confidence)>();
            var errors = new ConcurrentBag<string>();
            _globalSw = Stopwatch.StartNew();

            Parallel.ForEach(files, new ParallelOptions { MaxDegreeOfParallelism = threads }, filePath =>
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;
                var categorizer = categorizers.GetOrAdd(
                    threadId,
                    _ => new Categorization(model) { AllowUnknownCategory = true });

                try
                {
                    var itemSw = Stopwatch.StartNew();
                    using var attachment = new Attachment(filePath);
                    int idx = categorizer.GetBestCategory(Categories, attachment);
                    itemSw.Stop();

                    string category = idx == -1 ? "unknown" : Categories[idx];
                    double confidence = categorizer.Confidence;

                    // Copy to output
                    string destDir = Path.Combine(outputFolder, category);
                    Directory.CreateDirectory(destDir);
                    string destFile = GetUniquePath(destDir, Path.GetFileName(filePath));
                    File.Copy(filePath, destFile);

                    results.Add((category, confidence));
                    PrintResult(filePath, category, confidence, itemSw.ElapsedMilliseconds, threadId);
                }
                catch (Exception ex)
                {
                    errors.Add(ex.Message);
                    PrintError(filePath, ex.Message, threadId);
                }
            });

            _globalSw.Stop();

            // Summary
            Console.WriteLine($"\n=== Complete ===");
            Console.WriteLine($"Processed: {results.Count}/{_total} in {_globalSw.Elapsed.TotalSeconds:F1}s");
            Console.WriteLine($"Speed: {results.Count / _globalSw.Elapsed.TotalSeconds:F2} docs/sec");
            if (results.Count > 0)
            {
                Console.WriteLine($"Avg confidence: {results.Average(r => r.Confidence):P0}");
                Console.WriteLine($"Avg time/doc: {_globalSw.ElapsedMilliseconds / results.Count}ms");
            }
            if (errors.Count > 0)
                Console.WriteLine($"Errors: {errors.Count}");

            Console.WriteLine($"\nOutput: {outputFolder}");
        }

        static void PrintResult(string path, string category, double confidence, long ms, int threadId)
        {
            int n = Interlocked.Increment(ref _processed);
            long avgMs = _globalSw.ElapsedMilliseconds / n;

            lock (ConsoleLock)
            {
                Console.WriteLine($"[{n}/{_total}] [T{threadId:D2}] {Path.GetFileName(path)} -> {category} ({confidence:P0}) [{ms}ms] (avg: {avgMs}ms)");
            }
        }

        static void PrintError(string path, string error, int threadId)
        {
            int n = Interlocked.Increment(ref _processed);
            lock (ConsoleLock)
            {
                Console.WriteLine($"[{n}/{_total}] [T{threadId:D2}] {Path.GetFileName(path)} -> ERROR: {error}");
            }
        }

        static string GetUniquePath(string folder, string fileName)
        {
            string path = Path.Combine(folder, fileName);
            if (!File.Exists(path)) return path;

            string name = Path.GetFileNameWithoutExtension(fileName);
            string ext = Path.GetExtension(fileName);
            int i = 1;
            while (File.Exists(path))
                path = Path.Combine(folder, $"{name}_{i++}{ext}");
            return path;
        }
    }
}