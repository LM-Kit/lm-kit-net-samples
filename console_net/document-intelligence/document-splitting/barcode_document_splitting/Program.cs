using LMKit.Extraction.Barcodes;
using System.Text;

namespace barcode_document_splitting
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            // Set your license key here. A trial runs without one.
            //LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.Clear();
            WriteTitle();

            while (true)
            {
                // 1. The batch to split.
                string? inputPath = AskForBatch();

                if (inputPath == null)
                {
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                // 2. What a barcode means, and how the document's value is read from it.
                BarcodeSplitMode mode = AskForMode();
                string? valuePattern = AskForValuePattern();

                var options = new BarcodeSplittingOptions
                {
                    Mode = mode,
                    RemoveBlankPageAfterSeparator = true,
                    FileNameTemplate = "{value}_{index:000}"
                };

                try
                {
                    if (!string.IsNullOrEmpty(valuePattern))
                    {
                        options.ValuePattern = valuePattern;
                    }
                }
                catch (ArgumentException e)
                {
                    WriteError($"Invalid value pattern, the full barcode text is used instead. {e.Message}");
                }

                string outputDirectory = Path.Combine(
                    Path.GetDirectoryName(Path.GetFullPath(inputPath)) ?? ".",
                    Path.GetFileNameWithoutExtension(inputPath) + "_split");

                // 3. Split: scan every page, cut the batch, write one file per document.
                var splitter = new BarcodeSplitting(options);
                splitter.PageScanned += (sender, e) =>
                    Console.Write($"\rScanning page {e.ScannedCount}/{e.TotalToScan}...   ");

                try
                {
                    BarcodeSplittingResult result = splitter.SplitToFiles(inputPath, outputDirectory);
                    Console.WriteLine();
                    PrintResult(result, outputDirectory);
                }
                catch (Exception e)
                {
                    Console.WriteLine();
                    WriteError($"Unable to split '{inputPath}': {e.Message}");
                }

                Console.Write("\nPress Enter to split another batch, or type 'q' to quit: ");
                string again = Console.ReadLine() ?? string.Empty;

                if (string.Equals(again.Trim(), "q", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("\nDemo ended. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }

                Console.Clear();
                WriteTitle();
            }
        }

        private static void WriteTitle()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("LM-Kit Barcode Document Splitting Demo");
            Console.ResetColor();
            Console.WriteLine("Split a scanned batch into documents by barcodes. Deterministic: no model is loaded.\n");
        }

        private static string? AskForBatch()
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write("Assistant");
                Console.ResetColor();
                Console.Write(" - enter the path of a PDF, TIFF or image batch, press Enter for a generated sample, or 'q' to quit:\n> ");

                string path = (Console.ReadLine() ?? string.Empty).Trim().Trim('"');

                if (string.Equals(path, "q", StringComparison.OrdinalIgnoreCase))
                {
                    return null;
                }

                if (path.Length == 0)
                {
                    string sample = SampleBatch.Create();
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine($"\nSample batch written to '{sample}'.");
                    Console.WriteLine("It holds 4 documents behind QR separator sheets (INV-1001, PO-2001, INV-1002, INV-1001 again),");
                    Console.WriteLine("each sheet followed by its blank back, as a duplex scanner produces them.\n");
                    Console.ResetColor();
                    return sample;
                }

                if (File.Exists(path))
                {
                    Console.WriteLine();
                    return path;
                }

                WriteError($"File not found: '{path}'.\n");
            }
        }

        private static BarcodeSplitMode AskForMode()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Assistant");
            Console.ResetColor();
            Console.WriteLine(" - what does a barcode mean in this batch?\n");
            Console.WriteLine("0 - SeparatorPage        a sheet between two documents, removed from the output (default)");
            Console.WriteLine("1 - FirstPageOfDocument  the page opens a document and stays in it");
            Console.WriteLine("2 - LastPageOfDocument   the page closes a document and stays in it");
            Console.WriteLine("3 - ValueChange          a new document starts when the barcode value changes");
            Console.WriteLine("4 - GroupByValue         all pages with the same value form one document");
            Console.Write("\n> ");

            string choice = (Console.ReadLine() ?? string.Empty).Trim();
            Console.WriteLine();

            return choice switch
            {
                "1" => BarcodeSplitMode.FirstPageOfDocument,
                "2" => BarcodeSplitMode.LastPageOfDocument,
                "3" => BarcodeSplitMode.ValueChange,
                "4" => BarcodeSplitMode.GroupByValue,
                _ => BarcodeSplitMode.SeparatorPage
            };
        }

        private static string? AskForValuePattern()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("Assistant");
            Console.ResetColor();
            Console.WriteLine(" - optional regular expression extracting the document's value from the barcode text.");
            Console.WriteLine("The group named 'value' is kept, e.g. ^(?<value>[A-Z]+)- keeps INV or PO (try it with GroupByValue).");
            Console.Write("Press Enter to keep the full barcode text.\n> ");

            string pattern = (Console.ReadLine() ?? string.Empty).Trim();
            Console.WriteLine();

            return pattern.Length == 0 ? null : pattern;
        }

        private static void PrintResult(BarcodeSplittingResult result, string outputDirectory)
        {
            WriteHeader("Pages");

            foreach (BarcodePage page in result.Pages)
            {
                Console.Write($"  Page {page.PageNumber,3}  ");

                Console.ForegroundColor = page.Role switch
                {
                    BarcodePageRole.Content => ConsoleColor.White,
                    BarcodePageRole.KeptSeparator => ConsoleColor.Yellow,
                    _ => ConsoleColor.DarkGray
                };
                Console.Write($"{page.Role,-16}");
                Console.ResetColor();

                if (page.SegmentIndex >= 0)
                {
                    Console.Write($" doc {page.SegmentIndex + 1,-3}");
                }
                else
                {
                    Console.Write("        ");
                }

                if (page.IsBlank)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write(" (blank)");
                    Console.ResetColor();
                }

                foreach (BarcodeDetection barcode in page.Barcodes)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.Write($"  [{barcode.FormatName}: {barcode.Text}]");
                    Console.ResetColor();
                }

                foreach (BarcodeDetection barcode in page.IgnoredBarcodes)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  [ignored {barcode.FormatName}: {barcode.Text}]");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }

            WriteHeader("Documents");

            foreach (BarcodeSegment segment in result.Segments)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.Write($"  {segment.Index + 1,3}. ");
                Console.ResetColor();

                string pages = string.Join(", ", segment.PageIndexes.Select(i => i + 1));
                Console.Write($"{segment.Label,-20} pages {pages}");

                if (segment.OutputPath != null)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"  -> {Path.GetFileName(segment.OutputPath)}");
                    Console.ResetColor();
                }

                Console.WriteLine();
            }

            WriteHeader("Summary");
            Console.WriteLine($"  source           : {result.SourceName}");
            Console.WriteLine($"  pages            : {result.PageCount}");
            Console.WriteLine($"  documents        : {result.DocumentCount}");
            Console.WriteLine($"  separator pages  : {FormatPages(result.SeparatorPages)}");
            Console.WriteLine($"  discarded pages  : {FormatPages(result.DiscardedPages)}");
            Console.WriteLine($"  failed pages     : {(result.FailedPages.Count == 0 ? "none" : string.Join(", ", result.FailedPages.Select(p => $"{p.PageNumber} ({p.Error?.Message})")))}");
            Console.WriteLine($"  elapsed time     : {result.Elapsed.TotalMilliseconds:F0} ms");
            Console.WriteLine($"  output directory : {outputDirectory}");

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.WriteLine("  -----------------------------");
            Console.ResetColor();
        }

        private static string FormatPages(IReadOnlyList<int> pageIndexes)
            => pageIndexes.Count == 0 ? "none" : string.Join(", ", pageIndexes.Select(i => i + 1));

        private static void WriteHeader(string title)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"\n---------- {title} ----------");
            Console.ResetColor();
        }

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
