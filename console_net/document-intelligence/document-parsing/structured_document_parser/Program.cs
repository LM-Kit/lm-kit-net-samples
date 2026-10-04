using LMKit.Document.Layout;
using LMKit.Document.Parsing;
using LMKit.Model;
using System.Diagnostics;
using System.Text;

namespace structured_document_parser
{
    /// <summary>
    /// Parses PDFs, scans and images with LM-Kit.NET's AI document parser and exports the result
    /// in every standard format.
    ///
    /// The demo shows:
    ///   * One setting, the effort level (Low, Medium, High); the models are chosen and verified for you.
    ///   * Live page progress through the PageParsed event, and page-range selection.
    ///   * The typed elements of each page: role, reading order, bounds, confidence and content.
    ///   * Exports to Markdown, lossless JSON, semantic HTML and DocLang, with figure pictures.
    /// </summary>
    internal class Program
    {
        private static void Main()
        {
            // Set your license key here. A trial runs without one.
            //LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            PrintBanner();

            ParsingEffort effort = PromptEffort();
            bool keepImages = PromptYesNo("Keep the picture of every figure and chart? (y/N): ");

            DownloadModels(effort);

            Console.WriteLine("Loading the parser's models...");
            using var parser = new DocumentParser(effort) { IncludeFigureImages = keepImages };

            parser.PageParsed += (_, e) =>
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"  page {e.PageNumber} parsed ({e.PagesParsed}/{e.PageCount}) in {e.Elapsed.TotalSeconds:F1} s");
                Console.ResetColor();
            };

            while (true)
            {
                string? path = PromptPath();

                if (path == null)
                {
                    break;
                }

                Console.Write("Pages to parse (e.g. 1-3, 7; empty for all): ");
                string? pages = Console.ReadLine();

                try
                {
                    var stopwatch = Stopwatch.StartNew();
                    ParsedDocument document = parser.Parse(path, string.IsNullOrWhiteSpace(pages) ? null : pages);
                    stopwatch.Stop();

                    PrintElements(document);
                    Export(document, path, keepImages);

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"\n{document.Pages.Count} page(s), {document.Pages.Sum(p => p.Elements.Count)} elements in {stopwatch.Elapsed.TotalSeconds:F1} s.\n");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Parsing failed: {ex.Message}\n");
                    Console.ResetColor();
                }
            }
        }

        private static void PrintElements(ParsedDocument document)
        {
            foreach (ParsedPage page in document.Pages)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n--- Page {page.PageNumber}  ({page.Width:F0} x {page.Height:F0} {page.Unit})");
                Console.ResetColor();

                foreach (ParsedElement element in page.Elements)
                {
                    string kind = element.Content switch
                    {
                        TableContent => "table (HTML)",
                        FormulaContent => "formula (LaTeX)",
                        ChartContent chart => chart.HasPanels ? $"chart ({chart.Panels.Count} panels)" : "chart (data table)",
                        FigureContent => "figure",
                        _ => element.Category == LayoutElementCategory.Title ? "heading" : "text"
                    };

                    string preview = Preview(element.Content.Text);
                    Console.Write($"  {element.ReadingIndex,3}  {element.Category,-15} {kind,-20} ");
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"[{element.Bounds.Left:F0},{element.Bounds.Top:F0} {element.Bounds.Right:F0},{element.Bounds.Bottom:F0}] {element.Confidence:P0}  ");
                    Console.ResetColor();
                    Console.WriteLine(preview);
                }
            }
        }

        private static void Export(ParsedDocument document, string sourcePath, bool keepImages)
        {
            string folder = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath)) ?? ".", Path.GetFileNameWithoutExtension(sourcePath) + "_parsed");
            Directory.CreateDirectory(folder);
            string name = Path.GetFileNameWithoutExtension(sourcePath);

            File.WriteAllText(Path.Combine(folder, name + ".md"), document.ToMarkdown());
            File.WriteAllText(Path.Combine(folder, name + ".json"), document.ToJson());

            if (keepImages)
            {
                // Pictures as files beside the page, and one DocLang archive holding everything.
                document.SaveHtml(Path.Combine(folder, name + ".html"));
                document.SaveDocLang(Path.Combine(folder, name + ".dclg"));
                document.SaveDocLangArchive(Path.Combine(folder, name + ".dclx"));
            }
            else
            {
                File.WriteAllText(Path.Combine(folder, name + ".html"), document.ToHtml());
                File.WriteAllText(Path.Combine(folder, name + ".dclg"), document.ToDocLang());
            }

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\nExported Markdown, JSON, HTML and DocLang to {folder}");
            Console.ResetColor();
        }

        private static void DownloadModels(ParsingEffort effort)
        {
            foreach (ModelCard card in DocumentParser.GetModelCards(effort))
            {
                if (card.IsLocallyAvailable)
                {
                    continue;
                }

                Console.WriteLine($"Downloading {card.ModelName} ({card.FileSize / (1024 * 1024)} MB)...");
                card.Download(OnDownloadProgress);
                Console.WriteLine();
            }
        }

        private static bool OnDownloadProgress(string path, long? contentLength, long bytesRead)
        {
            if (contentLength.HasValue && contentLength.Value > 0)
            {
                Console.Write($"\r  {(double)bytesRead / contentLength.Value * 100:F1}%   ");
            }

            return true;
        }

        private static ParsingEffort PromptEffort()
        {
            Console.WriteLine("Choose the effort level:");
            Console.WriteLine("  1 - Low     fastest, for high-volume ingestion");
            Console.WriteLine("  2 - Medium  balanced time and fidelity (default)");
            Console.WriteLine("  3 - High    most faithful tables, charts and headings");
            Console.Write("> ");

            return Console.ReadLine()?.Trim() switch
            {
                "1" => ParsingEffort.Low,
                "3" => ParsingEffort.High,
                _ => ParsingEffort.Medium
            };
        }

        private static bool PromptYesNo(string question)
        {
            Console.Write(question);
            return string.Equals(Console.ReadLine()?.Trim(), "y", StringComparison.OrdinalIgnoreCase);
        }

        private static string? PromptPath()
        {
            while (true)
            {
                Console.Write("\nDocument to parse (PDF or image; empty to quit): ");
                string? input = Console.ReadLine()?.Trim().Trim('"');

                if (string.IsNullOrEmpty(input))
                {
                    return null;
                }

                if (File.Exists(input))
                {
                    return input;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("File not found.");
                Console.ResetColor();
            }
        }

        private static string Preview(string text)
        {
            string line = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ').Trim();
            return line.Length <= 60 ? line : line.Substring(0, 57) + "...";
        }

        private static void PrintBanner()
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== LM-Kit.NET: Structured Document Parser ===");
            Console.ResetColor();
            Console.WriteLine("PDFs, scans and images parsed into typed elements, exported as Markdown, JSON, HTML and DocLang.\n");
        }
    }
}
