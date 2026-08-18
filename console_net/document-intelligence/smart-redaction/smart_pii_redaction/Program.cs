using LMKit.Data;
using LMKit.Document.Conversion;
using LMKit.Document.Pdf;
using LMKit.Extraction.Ocr;
using LMKit.Model;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using PiiEntity = LMKit.TextAnalysis.PiiExtraction.PiiExtractedEntity;

namespace smart_pii_redaction
{
    /// <summary>
    /// Smart Redaction: detect PII with an on-device model, let a human review the
    /// suggestions, then permanently remove the approved values from the PDF.
    ///
    /// Input is a PDF (used directly) or an image (converted to a searchable PDF first).
    /// Detected entities carry page-accurate bounding boxes (occurrences); those boxes
    /// are what the redactor uses to place each mark.
    ///
    /// Pipeline: IDENTIFY (PiiExtraction) -> REVIEW (human-in-the-loop) -> REMOVE (PdfRedactor) -> VERIFY.
    /// </summary>
    internal static class Program
    {
        private static bool _isDownloading;

        private static readonly string[] ImageExtensions =
            { ".png", ".jpg", ".jpeg", ".bmp", ".tif", ".tiff", ".webp", ".gif" };

        private static void Main()
        {
            // Set an optional license key here if available.
            // A free community license can be obtained from: https://lm-kit.com/products/community-edition/
            LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;
            Console.Clear();

            WriteColor("=== LM-Kit.NET Smart Redaction ===", ConsoleColor.Cyan);
            Console.WriteLine("Detect PII, review it, then permanently remove it from a PDF. 100% on-device.\n");

            LM model = SelectAndLoadModel();

            while (true)
            {
                string? inputPath = SelectDocument();
                if (inputPath == null)
                {
                    break;
                }

                try
                {
                    ProcessDocument(model, inputPath);
                }
                catch (Exception ex)
                {
                    WriteColor($"\nError: {ex.Message}", ConsoleColor.Red);
                }

                Console.Write("\nProcess another document? [Y/n] ");
                if ((Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant() == "n")
                {
                    break;
                }
            }
        }

        private static void ProcessDocument(LM model, string inputPath)
        {
            // Normalize the input to a PDF that carries a positioned text layer:
            //   - PDF   -> used directly.
            //   - image -> converted to a searchable PDF (OCR text layer) so the
            //              redactor has bounding boxes to target and can scrub the pixels.
            string pdfPath = EnsurePdf(inputPath);

            // 1) IDENTIFY: detect PII across every page, with page-accurate bounding boxes.
            var extractor = Configuration.CreateExtractor(model);

            WriteColor($"\nIdentifying PII in {Path.GetFileName(pdfPath)}...", ConsoleColor.Cyan);
            var stopwatch = Stopwatch.StartNew();
            List<PiiEntity> entities;
            using (var document = new Attachment(pdfPath))
            {
                entities = extractor.Extract(document);
            }
            stopwatch.Stop();

            if (entities.Count == 0)
            {
                WriteColor("No PII detected. Nothing to redact.", ConsoleColor.Yellow);
                return;
            }

            WriteColor(
                $"Detected {entities.Count} PII item(s) in {stopwatch.Elapsed.TotalSeconds:0.0}s " +
                $"(overall confidence {extractor.Confidence:0.00}).\n",
                ConsoleColor.Green);

            // 2) REVIEW: a human keeps or drops each suggestion (accountability).
            List<PiiEntity> approved = Review(entities);
            if (approved.Count == 0)
            {
                WriteColor("\nNo items approved for redaction. The document is left unchanged.", ConsoleColor.Yellow);
                return;
            }

            // 3) REMOVE: redact each approved occurrence by its bounding box.
            var request = new PdfRedactionRequest();
            int boxes = 0;
            foreach (PiiEntity entity in approved)
            {
                // Each occurrence is a TextRegion carrying the entity's bounding box on the
                // page. FromRegion turns it into redaction areas that hug the matched glyphs.
                foreach (var occurrence in entity.Occurrences)
                {
                    try
                    {
                        IReadOnlyList<PdfRedactionArea> areas = PdfRedactionArea.FromRegion(occurrence);
                        request.Areas.AddRange(areas);
                        boxes += areas.Count;
                    }
                    catch (ArgumentException)
                    {
                        // Occurrence without usable geometry: the search term below still catches it.
                    }
                }

                // Safety net: remove every textual occurrence of the value too, so nothing slips through.
                request.SearchTerms.Add(entity.Value);
            }

            byte[] source = File.ReadAllBytes(pdfPath);
            PdfRedactionResult result = PdfRedactor.RedactToBytes(source, request);

            string outputPath = Path.Combine(
                Path.GetDirectoryName(Path.GetFullPath(pdfPath))!,
                Path.GetFileNameWithoutExtension(pdfPath) + "_redacted.pdf");
            File.WriteAllBytes(outputPath, result.Data);

            WriteColor(
                $"\nRedacted {boxes} bounding box(es): {result.Report.RemovedGlyphs} glyphs, " +
                $"{result.Report.EditedImages} image region(s), and {result.Report.RemovedAnnotations} annotation(s) " +
                $"across {result.Report.PagesProcessed} page(s).",
                ConsoleColor.Green);
            WriteColor($"Saved: {outputPath}", ConsoleColor.Green);

            // 4) VERIFY: prove the approved values can no longer be found.
            VerifyRedaction(result.Data, approved);

            OpenDocument(outputPath);
        }

        /// <summary>
        /// Returns a path to a PDF. PDFs pass through; images are converted to a
        /// searchable PDF (OCR text layer) so redaction has bounding boxes to target.
        /// </summary>
        private static string EnsurePdf(string inputPath)
        {
            string ext = Path.GetExtension(inputPath).ToLowerInvariant();

            if (ext == ".pdf")
            {
                return inputPath;
            }

            if (Array.IndexOf(ImageExtensions, ext) >= 0)
            {
                WriteColor("Converting image to a searchable PDF (OCR)...", ConsoleColor.DarkGray);
                string pdfPath = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(inputPath))!,
                    Path.GetFileNameWithoutExtension(inputPath) + "_searchable.pdf");
                ImageToSearchablePdf.Convert(inputPath, new LMKitOcr(), pdfPath);
                return pdfPath;
            }

            throw new NotSupportedException(
                $"Unsupported input '{ext}'. Provide a PDF or an image (png, jpg, tiff, bmp, webp, gif).");
        }

        // =====================================================================================
        //  Human-in-the-loop review
        // =====================================================================================

        private static List<PiiEntity> Review(List<PiiEntity> entities)
        {
            WriteColor("Review detected PII (human-in-the-loop):", ConsoleColor.White);
            Console.WriteLine("  [Enter]/y = redact    n = keep    a = redact all remaining    k = keep all remaining\n");

            var approved = new List<PiiEntity>();
            bool redactRest = false;
            bool keepRest = false;

            for (int i = 0; i < entities.Count; i++)
            {
                PiiEntity entity = entities[i];
                int page = entity.Occurrences.Count > 0 ? entity.Occurrences[0].PageIndex + 1 : 0;
                string location = page > 0 ? $"page {page}" : "unresolved";

                Console.Write($"  [{i + 1}/{entities.Count}] ");
                WriteColor(entity.EntityDefinition.Label, ConsoleColor.Magenta, addNL: false);
                Console.Write($"  \"{entity.Value}\"  ");
                WriteColor(
                    $"(confidence {entity.Confidence:0.00}, {entity.Occurrences.Count} occurrence(s), {location})",
                    ConsoleColor.DarkGray,
                    addNL: false);

                string choice;
                if (redactRest)
                {
                    choice = "y";
                    Console.WriteLine("  -> redact");
                }
                else if (keepRest)
                {
                    choice = "n";
                    Console.WriteLine("  -> keep");
                }
                else
                {
                    Console.Write("   redact? [Y/n/a/k] ");
                    choice = (Console.ReadLine() ?? string.Empty).Trim().ToLowerInvariant();
                }

                switch (choice)
                {
                    case "a":
                        redactRest = true;
                        approved.Add(entity);
                        break;
                    case "k":
                        keepRest = true;
                        break;
                    case "n":
                        break;
                    default: // Enter or "y": redact by default (secure default).
                        approved.Add(entity);
                        break;
                }
            }

            WriteColor($"\nApproved {approved.Count} of {entities.Count} item(s) for redaction.", ConsoleColor.White);
            return approved;
        }

        // =====================================================================================
        //  Verification
        // =====================================================================================

        private static void VerifyRedaction(byte[] redactedPdf, List<PiiEntity> approved)
        {
            WriteColor("\nVerifying the redacted output (fresh, cache-free search)...", ConsoleColor.Cyan);

            using var attachment = new Attachment(redactedPdf, "redacted.pdf");
            int leaks = 0;

            foreach (string value in approved.Select(e => e.Value).Distinct())
            {
                int hits = PdfSearch.FindText(attachment, value).TotalMatches;
                if (hits > 0)
                {
                    leaks++;
                    WriteColor($"  LEAK: \"{value}\" still found {hits} time(s).", ConsoleColor.Red);
                }
            }

            if (leaks == 0)
            {
                WriteColor("  All approved values are unrecoverable by text extraction.", ConsoleColor.Green);
            }
        }

        // =====================================================================================
        //  Model + document selection
        // =====================================================================================

        private static LM SelectAndLoadModel()
        {
            Console.WriteLine("Select a model for PII detection:\n");
            Console.WriteLine("0 - Alibaba Qwen 3.5 4B (recommended)   (~3.5 GB VRAM)");
            Console.WriteLine("1 - Alibaba Qwen 3.5 2B                 (~2 GB VRAM)");
            Console.WriteLine("2 - Alibaba Qwen 3.5 9B                 (~7 GB VRAM)");
            Console.WriteLine("3 - Google Gemma 4 E4B                  (~6 GB VRAM)");
            Console.WriteLine("4 - Mistral Ministral 3 8B              (~6.5 GB VRAM)");
            Console.WriteLine("5 - Z.ai GLM-V 4.6 Flash 10B            (~7 GB VRAM, strong on scans)");
            Console.WriteLine("6 - Alibaba Qwen 3.8 27B                (~18 GB VRAM, highest accuracy)");
            Console.WriteLine("Or enter a custom model URI or model ID");
            Console.Write("\n> ");

            string input = (Console.ReadLine() ?? "0").Trim();
            string? modelId = input switch
            {
                "" or "0" => "qwen3.5:4b",
                "1" => "qwen3.5:2b",
                "2" => "qwen3.5:9b",
                "3" => "gemma4:e4b",
                "4" => "ministral3:8b",
                "5" => "glm-4.6v-flash",
                "6" => "qwen3.8:27b",
                _ => null
            };

            Console.WriteLine();

            LM model = modelId != null
                ? LM.LoadFromModelID(modelId, downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress)
                : new LM(new Uri(input.Trim('"')), downloadingProgress: OnDownloadProgress, loadingProgress: OnLoadProgress);

            if (_isDownloading)
            {
                Console.WriteLine();
                _isDownloading = false;
            }

            return model;
        }

        private static string? SelectDocument()
        {
            Console.WriteLine("\nSelect a document to redact:\n");
            Console.WriteLine("0 - Bundled sample: account_application.pdf (fictitious PII)");
            Console.WriteLine("Or enter a path to your own PDF or image (png, jpg, tiff, ...)");
            Console.WriteLine("(press q to quit)");
            Console.Write("\n> ");

            string input = (Console.ReadLine() ?? "0").Trim().Trim('"');

            if (input.ToLowerInvariant() == "q")
            {
                return null;
            }

            if (input.Length == 0 || input == "0")
            {
                return Path.Combine(AppContext.BaseDirectory, "examples", "account_application.pdf");
            }

            if (!File.Exists(input))
            {
                WriteColor($"File not found: {input}", ConsoleColor.Yellow);
                return SelectDocument();
            }

            return input;
        }

        // =====================================================================================
        //  Console helpers
        // =====================================================================================

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            _isDownloading = true;
            Console.Write(contentLength.HasValue
                ? $"\rDownloading model: {(double)bytesRead / contentLength.Value * 100:0.0}%   "
                : $"\rDownloading model: {bytesRead / 1024.0 / 1024.0:0.0} MB   ");
            return true;
        }

        private static bool OnLoadProgress(float progress)
        {
            if (_isDownloading)
            {
                Console.WriteLine();
                _isDownloading = false;
            }
            Console.Write($"\rLoading model: {progress * 100:0}%   ");
            return true;
        }

        private static void WriteColor(string text, ConsoleColor color, bool addNL = true)
        {
            Console.ForegroundColor = color;
            if (addNL)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.Write(text);
            }
            Console.ResetColor();
        }

        private static void OpenDocument(string filePath)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", filePath);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", filePath);
                }
            }
            catch
            {
                // Opening the viewer is a convenience only; ignore failures on headless hosts.
            }
        }
    }
}
