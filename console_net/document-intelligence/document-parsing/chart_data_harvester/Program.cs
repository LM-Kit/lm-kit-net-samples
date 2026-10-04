using LMKit.Document.Parsing;
using LMKit.Media.Image;
using LMKit.Model;
using System.Text;

namespace chart_data_harvester
{
    /// <summary>
    /// Harvests the data behind every chart of a folder of documents (PDFs, scans, images) with LM-Kit.NET's
    /// AI document parser: each chart comes back as its data table, written as CSV beside the
    /// chart's picture, and an index lists every chart found with its page and title.
    /// </summary>
    internal class Program
    {
        private static void Main()
        {
            // Set your license key here. A trial runs without one.
            //LMKit.Licensing.LicenseManager.SetLicenseKey("");

            Console.InputEncoding = Encoding.UTF8;
            Console.OutputEncoding = Encoding.UTF8;

            if (!Console.IsOutputRedirected)
            {
                Console.Clear();
            }
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("=== LM-Kit.NET: Chart Data Harvester ===");
            Console.ResetColor();
            Console.WriteLine("Every chart of a folder of documents, read as data and exported as CSV.\n");

            string folder = PromptFolder();
            string output = Path.Combine(folder, "charts");
            Directory.CreateDirectory(output);

            // Charts are where fidelity matters most: the highest effort level reads them best.
            const ParsingEffort Effort = ParsingEffort.High;
            DownloadModels(Effort);

            Console.WriteLine("Loading the parser's models...");
            using var parser = new DocumentParser(Effort) { IncludeFigureImages = true };

            var index = new StringBuilder("document,page,chart,title,csv,picture\n");
            int total = 0;

            foreach (string file in Directory.GetFiles(folder).Where(IsSupported))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"\n{Path.GetFileName(file)}");
                Console.ResetColor();

                ParsedDocument document;

                try
                {
                    document = parser.Parse(file);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  skipped: {ex.Message}");
                    Console.ResetColor();
                    continue;
                }

                foreach (ParsedPage page in document.Pages)
                {
                    int chartIndex = 0;

                    for (int e = 0; e < page.Elements.Count; e++)
                    {
                        ParsedElement element = page.Elements[e];

                        if (!(element.Content is ChartContent chart) || chart.IsEmpty)
                        {
                            continue;
                        }

                        chartIndex++;
                        total++;

                        string stem = $"{Path.GetFileNameWithoutExtension(file)}-p{page.PageNumber}-c{chartIndex}";
                        string title = TitleOf(page, e, chart);

                        // A figure of several plots carries one table per panel.
                        var tables = chart.HasPanels
                            ? chart.Panels.Select((panel, p) => (Suffix: $"-{p + 1}", Table: panel.Table)).ToList()
                            : new List<(string Suffix, string Table)> { (string.Empty, chart.Text) };

                        foreach ((string suffix, string table) in tables)
                        {
                            File.WriteAllText(Path.Combine(output, stem + suffix + ".csv"), ToCsv(table));
                        }

                        string picture = string.Empty;

                        using (ImageBuffer? image = element.GetImage())
                        {
                            if (image != null)
                            {
                                picture = stem + ".png";
                                image.SaveAsPng(Path.Combine(output, picture));
                            }
                        }

                        index.Append(Csv(Path.GetFileName(file))).Append(',')
                            .Append(page.PageNumber).Append(',')
                            .Append(chartIndex).Append(',')
                            .Append(Csv(title)).Append(',')
                            .Append(Csv(string.Join(" ", tables.Select(t => stem + t.Suffix + ".csv")))).Append(',')
                            .Append(Csv(picture)).Append('\n');

                        string shown = title.Length == 0 ? "untitled chart" : title.Length <= 80 ? title : title.Substring(0, 77) + "...";
                        Console.WriteLine($"  page {page.PageNumber}: {shown} ({tables.Count} table{(tables.Count == 1 ? "" : "s")})");
                    }
                }

                // A reviewable page: each chart's picture beside its data table.
                document.SaveHtml(Path.Combine(output, Path.GetFileNameWithoutExtension(file) + ".html"));
            }

            File.WriteAllText(Path.Combine(output, "index.csv"), index.ToString());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n{total} chart(s) harvested into {output}");
            Console.ResetColor();
        }

        /// <summary>
        /// The chart's title: the one it prints for itself, else the caption element set next to it
        /// on the page (a caption above or below the plot), else the title printed inside its box.
        /// </summary>
        private static string TitleOf(ParsedPage page, int index, ChartContent chart)
        {
            if (chart.Caption.Length > 0)
            {
                return chart.Caption;
            }

            foreach (int neighbour in new[] { index + 1, index - 1 })
            {
                if (neighbour >= 0 && neighbour < page.Elements.Count
                    && page.Elements[neighbour].Category == LMKit.Document.Layout.LayoutElementCategory.FigureCaption)
                {
                    return page.Elements[neighbour].Content.Text.Replace("*", "").Replace('\n', ' ').Trim();
                }
            }

            return chart.PieceTitle;
        }

        /// <summary>A Markdown pipe table as CSV: the separator row dropped, cells quoted when needed.</summary>
        private static string ToCsv(string markdownTable)
        {
            var csv = new StringBuilder();

            foreach (string line in markdownTable.Split('\n'))
            {
                string row = line.Trim();

                if (row.Length == 0 || row.Replace("|", "").Replace("-", "").Replace(":", "").Trim().Length == 0)
                {
                    continue;
                }

                csv.AppendLine(string.Join(",", row.Trim('|').Split('|').Select(cell => Csv(cell.Trim()))));
            }

            return csv.ToString();
        }

        private static string Csv(string value)
            => value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;

        private static bool IsSupported(string path)
            => LMKit.Data.Attachment.IsSupportedFormat(path);

        private static void DownloadModels(ParsingEffort effort)
        {
            foreach (ModelCard card in DocumentParser.GetModelCards(effort))
            {
                if (card.IsLocallyAvailable)
                {
                    continue;
                }

                Console.WriteLine($"Downloading {card.ModelName} ({card.FileSize / (1024 * 1024)} MB)...");
                card.Download((_, length, read) =>
                {
                    if (length > 0)
                    {
                        Console.Write($"\r  {(double)read / length.Value * 100:F1}%   ");
                    }

                    return true;
                });
                Console.WriteLine();
            }
        }

        private static string PromptFolder()
        {
            while (true)
            {
                Console.Write("Folder of PDFs, scans or images: ");
                string? input = Console.ReadLine()?.Trim().Trim('"');

                if (!string.IsNullOrEmpty(input) && Directory.Exists(input))
                {
                    return input;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Folder not found.");
                Console.ResetColor();
            }
        }
    }
}
